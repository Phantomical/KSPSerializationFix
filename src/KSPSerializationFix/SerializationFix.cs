using System;
using System.Runtime.InteropServices;
using System.Text;
using KSPSerializationFix.Platform;
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

internal struct AssemblyInfo
{
    public IntPtr image;
    public string name;
}

internal sealed class UnsupportedPlatformException(string message) : Exception(message) { }

internal static class SerializationFix
{
    internal static void RegisterAssemblies(AssemblyInfo[] infos)
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
                    WinDbg.RegisterAssemblies(infos);
                else
                    WinRel.RegisterAssemblies(infos);
                break;

            case RuntimePlatform.LinuxPlayer:
                if (isDbgBuild)
                    LinuxDbg.RegisterAssemblies(infos);
                else
                    LinuxRel.RegisterAssemblies(infos);
                break;

            case RuntimePlatform.OSXPlayer:
                if (isDbgBuild)
                    MacDbg.RegisterAssemblies(infos);
                else
                    MacRel.RegisterAssemblies(infos);
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
    >(TPlatform platform, AssemblyInfo[] infos)
        where TPlatform : IPlatform<TString>
        where TString : unmanaged
        where TStringArray : unmanaged, IDynamicArray<TString>
        where TIntArray : unmanaged, IDynamicArray<int>
        where TPtrArray : unmanaged, IDynamicArray<IntPtr>
        where TMonoManager : unmanaged, IMonoManager<TString, TStringArray, TIntArray, TPtrArray>
    {
        if (infos == null)
            throw new ArgumentNullException(nameof(infos));
        if (infos.Length == 0)
            return;

        IntPtr mgrPtr = platform.GetMonoManagerPointer();
        if (mgrPtr == IntPtr.Zero)
            throw new InvalidOperationException("Could not access Unity's internal MonoManager");

        TMonoManager* mgr = (TMonoManager*)(void*)mgrPtr;

        if (!mgr->AreAssembliesLoaded)
            throw new InvalidOperationException(
                "MonoManager assemblies not loaded yet; called too early"
            );

        int defaultType = 0;
        ref TIntArray typesArr = ref mgr->AssemblyTypes;
        if (typesArr.Size > 0 && typesArr.Ptr != IntPtr.Zero)
        {
            int* existing = (int*)typesArr.Ptr;
            defaultType = existing[typesArr.Size - 1];
        }

        var names = new TString[infos.Length];
        var types = new int[infos.Length];
        var images = new IntPtr[infos.Length];
        var paths = new int[infos.Length];

        for (int i = 0; i < infos.Length; i++)
        {
            string name = infos[i].name ?? string.Empty;
            byte[] utf8 = Encoding.UTF8.GetBytes(name);

            IntPtr buf = Marshal.AllocHGlobal(utf8.Length + 1);
            if (utf8.Length > 0)
                Marshal.Copy(utf8, 0, buf, utf8.Length);
            Marshal.WriteByte(buf, utf8.Length, 0);

            names[i] = platform.CreateString(buf, utf8.Length);
            types[i] = defaultType;
            images[i] = infos[i].image;
            paths[i] = -1;
        }

        Append(ref mgr->AssemblyNames, names);
        Append(ref mgr->AssemblyTypes, types);
        Append(ref mgr->ScriptImages, images);
        Append(ref mgr->AssemblyMonoPathsIndex, paths);
    }

    /// <summary>
    /// Append a sequence of elements to a Unity <c>dynamic_array&lt;T&gt;</c>.
    /// Always reallocates a fresh buffer and memcpys the existing contents.
    /// The new buffer is marked borrowed (low bit of capacity = 1) so
    /// Unity's <c>~dynamic_array</c> skips both per-element destruction and
    /// the buffer free. The previous pointer is intentionally leaked -
    /// Unity allocated it via a MemLabelId-aware allocator we cannot match
    /// from C#.
    ///
    /// Capacity stores the raw count with the LSB as flag (verified against
    /// the move ctor at UnityPlayer.dll +0x134DB0 which copies it verbatim).
    /// We round the allocation up to <c>(newSize | 1)</c> so the 1-slot
    /// slack implied by an even newSize is real backing memory, in case any
    /// code path treats capacity literally and writes past size. Caller
    /// must ensure no other code is concurrently reading the array.
    /// </summary>
    private static unsafe void Append<TArray, T>(ref TArray array, T[] values)
        where TArray : unmanaged, IDynamicArray<T>
        where T : unmanaged
    {
        if (values == null)
            throw new ArgumentNullException(nameof(values));
        if (values.Length == 0)
            return;

        int elemSize = sizeof(T);
        ulong oldSize = array.Size;
        IntPtr oldPtr = array.Ptr;
        ulong addCount = (ulong)values.Length;
        ulong newSize = oldSize + addCount;
        ulong newCapacity = newSize | 1UL;
        long bytes = checked((long)newCapacity * elemSize);
        IntPtr newBuf = Marshal.AllocHGlobal((IntPtr)bytes);

        if (oldPtr != IntPtr.Zero && oldSize > 0)
        {
            Buffer.MemoryCopy((void*)oldPtr, (void*)newBuf, bytes, (long)oldSize * elemSize);
        }

        fixed (T* src = values)
        {
            Buffer.MemoryCopy(
                src,
                (byte*)newBuf + (long)oldSize * elemSize,
                bytes - (long)oldSize * elemSize,
                (long)addCount * elemSize
            );
        }

        array.Ptr = newBuf;
        array.Size = newSize;
        array.Capacity = newCapacity;
    }
}
