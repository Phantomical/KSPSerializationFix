using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using KSPSerializationFix.Platform;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Profiling;

// Making serialization work for arbitrary DLLs
// ============================================
// When Unity goes to serialize a type it looks up its info from a list of DLLs
// that are baked into release bundle that is made for the game. This means that
// [Serializable] doesn't work for any DLLs other than the ones included in that
// built-in list.
//
// We would like to change that.
//
// In order to do that we need to somehow reach in and change that internal
// array. Luckily, for KSP we can get away with only supporting exactly one
// version of unity - 2019.4.18f1, which means that we can hardcode offsets
// to internal methods and properties.
//
// Within Unity there is a MonoManager class that holds a couple of arrays
// that are how unity finds the relevant assembly. We access that by forging
// a function pointer to the internal GetMonoManager function, then calling
// it. Once we have that, we can append the appropriate data for the assembly
// to 4 dynamic_array<T> fields within MonoManager.
//
// These are:
// 1. dynamic_array<basic_string> m_AssemblyNames
//
//    This contains the display/lookup names of every assemly in the list.
//    Unity searches this by name when looking for an assembly.
//
// 2. dynamic_array<AssemblyType> m_AssemblyTypes;
//
//    An enum that indicates the type of the assembly in the list. The
//    individual values are
//    - 0 = User     - Assembly-CSharp, any other user dlls
//    - 1 = Internal - internal unity engine dlls
//    - 2 = System   - i.e. mscorlib, System.*, etc
//    - 3 = Editor   - not used at runtime
//
//    All the entries we want to add can be marked as 0.
//
// 3. dynamic_array<ScriptingImagePtr> m_ScriptImages
//
//    On Mono backends this is an array of MonoImage* pointers that are
//    used to actually access the assembly.
//
// 4. dynamic_array<int> m_AssemblyMonoPathsIndex
//
//    An index into Unity's list of search directories. We set this to -1,
//    and it is not used by unity anywhere else.
//
// As long as nothing has been serialized for an assembly we can modify this
// and it will transparently be picked up by unity.

namespace KSPSerializationFix;

internal enum AssemblyType : int
{
    User = 0,
    Internal = 1,
    System = 2,
    Editor = 3,
}

internal sealed class UnsupportedPlatformException(string message) : Exception(message) { }

internal static class SerializationFix
{
    internal static void RegisterDependentAssemblies()
    {
        var self = typeof(SerializationFix).Assembly;
        var lself = AssemblyLoader.loadedAssemblies.First(asm => asm.assembly == self);

        var targets = AssemblyLoader
            .loadedAssemblies.Where(asm => asm.deps.Contains(lself))
            .Select(asm => asm.assembly)
            .Where(asm => asm is not null)
            .ToArray();

        RegisterAssemblies(targets);
    }

    internal static void RegisterAssemblies(Assembly[] assemblies)
    {
        bool isDbgBuild = Profiler.supported;

        if (Application.unityVersion != "2019.4.18f1")
            throw new UnsupportedPlatformException(
                $"unity version {Application.unityVersion} is not supported by SerializationFix"
            );

        switch (Application.platform)
        {
            case RuntimePlatform.WindowsPlayer:
                if (isDbgBuild)
                    WinDbg.RegisterAssemblies(assemblies);
                else
                    WinRel.RegisterAssemblies(assemblies);
                break;

            case RuntimePlatform.LinuxPlayer:
                if (isDbgBuild)
                    LinuxDbg.RegisterAssemblies(assemblies);
                else
                    LinuxRel.RegisterAssemblies(assemblies);
                break;

            case RuntimePlatform.OSXPlayer:
                if (isDbgBuild)
                    MacDbg.RegisterAssemblies(assemblies);
                else
                    MacRel.RegisterAssemblies(assemblies);
                break;

            default:
                throw new UnsupportedPlatformException(
                    $"platform {Application.platform} is not supported by SerializationFix"
                );
        }
    }

    /// <summary>
    /// Modify unity internals so as to register these assemblies for serialization.
    /// </summary>
    internal static unsafe void RegisterAssemblies<
        TPlatform,
        TString,
        TStringArray,
        TIntArray,
        TPtrArray,
        TMonoManager
    >(TPlatform platform, Assembly[] assemblies)
        where TPlatform : IPlatform<TString>
        where TString : unmanaged
        where TStringArray : unmanaged, IDynamicArray<TString>
        where TIntArray : unmanaged, IDynamicArray<int>
        where TPtrArray : unmanaged, IDynamicArray<IntPtr>
        where TMonoManager : unmanaged, IMonoManager<TString, TStringArray, TIntArray, TPtrArray>
    {
        if (assemblies == null)
            throw new ArgumentNullException(nameof(assemblies));
        if (assemblies.Length == 0)
            return;

        IntPtr mgrPtr = platform.GetMonoManagerPointer();
        if (mgrPtr == IntPtr.Zero)
            throw new InvalidOperationException("Could not access Unity's internal MonoManager");

        TMonoManager* mgr = (TMonoManager*)(void*)mgrPtr;

        if (!mgr->AreAssembliesLoaded)
            throw new InvalidOperationException(
                "MonoManager assemblies not loaded yet; called too early"
            );

        var names = new TString[assemblies.Length];
        var types = new int[assemblies.Length];
        var images = new IntPtr[assemblies.Length];
        var paths = new int[assemblies.Length];

        for (int i = 0; i < assemblies.Length; i++)
        {
            Assembly assembly = assemblies[i];
            string name = assembly.GetName().Name ?? string.Empty;
            byte[] utf8 = Encoding.UTF8.GetBytes(name);

            IntPtr buf = Marshal.AllocHGlobal(utf8.Length + 1);
            if (utf8.Length > 0)
                Marshal.Copy(utf8, 0, buf, utf8.Length);
            Marshal.WriteByte(buf, utf8.Length, 0);

            names[i] = platform.CreateString(buf, utf8.Length);
            types[i] = (int)AssemblyType.User;
            images[i] = Mono.GetMonoImage(assembly);
            paths[i] = -1;
        }

        Append(ref mgr->AssemblyNames, names);
        Append(ref mgr->AssemblyTypes, types);
        Append(ref mgr->ScriptImages, images);
        Append(ref mgr->AssemblyMonoPathsIndex, paths);
    }

    /// <summary>
    /// Our own implementation of <c>basic_array&lt;T&gt;::append</c>.
    /// </summary>
    private static unsafe void Append<TArray, T>(ref TArray array, T[] values)
        where TArray : unmanaged, IDynamicArray<T>
        where T : unmanaged
    {
        if (values == null)
            throw new ArgumentNullException(nameof(values));
        if (values.Length == 0)
            return;

        ulong oldSize = array.Size;
        IntPtr oldPtr = array.Ptr;
        ulong addCount = (ulong)values.Length;
        ulong newSize = oldSize + addCount;

        // Unity's dynamic_array considers an odd capacity to mean
        // "this array is borrowed and should not be freed."
        //
        // We're using an entirely different allocator from what unity uses, so
        // we make sure to put the array in a state that is considered to be
        // borrowed.
        ulong newCapacity = newSize | 1UL;
        long bytes = (long)newCapacity * sizeof(T);
        IntPtr newBuf = Marshal.AllocHGlobal((IntPtr)bytes);
        if (newBuf == IntPtr.Zero)
            throw new OutOfMemoryException("failed to allocate memory");

        if (oldPtr != IntPtr.Zero && oldSize > 0)
        {
            UnsafeUtility.MemCpy((void*)newBuf, (void*)oldPtr, (long)oldSize * sizeof(T));
        }

        fixed (T* src = values)
        {
            UnsafeUtility.MemCpy(
                (byte*)newBuf + (long)oldSize * sizeof(T),
                src,
                (long)addCount * sizeof(T)
            );
        }

        array.Ptr = newBuf;
        array.Size = newSize;
        array.Capacity = newCapacity;
    }
}

// We run ourselves as one of the first vessel modules to be registered, so we
// can be sure that we run before any other class might need to deserialize
// anything.
internal class SerializationFixInjector : VesselModule
{
    protected override void OnAwake()
    {
        SerializationFix.RegisterDependentAssemblies();
    }
}

// Clean up our temporary vessel module, since actually adding it to any ships
// would be a waste of resources.
[KSPAddon(KSPAddon.Startup.Instantly, once: true)]
internal class SerializationFixCleanup : MonoBehaviour
{
    void Awake()
    {
        VesselModuleManager.RemoveModuleOfType(typeof(SerializationFixInjector));
        Destroy(this);
    }
}
