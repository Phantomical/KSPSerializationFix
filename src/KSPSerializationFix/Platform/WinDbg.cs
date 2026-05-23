// Windows debug (development) Unity 2019.4.18f1 player layout.
// Verified from UnityPlayer_Win64_development_mono_x64.pdb
// (GUID 98FC1713-97AE-49F3-9514-12F65025E2AB, age 1).
//
// MSVC ABI + dev MemLabelId expansion. Every MonoManager field sits 0x30+
// later than its WinRel counterpart. Embedded dynamic_array / basic_string
// layouts are shared across dev variants - see Debug.cs.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace KSPSerializationFix.Platform;

internal static class WinDbg
{
    /// <summary>MonoManager - sizeof 712 (verified from dev PDB).</summary>
    [StructLayout(LayoutKind.Explicit, Size = 712)]
    internal struct MonoManager
        : IMonoManager<
            Debug.BasicString,
            Debug.DynamicArray<Debug.BasicString>,
            Debug.DynamicArray<int>,
            Debug.DynamicArray<IntPtr>
        >
    {
        [FieldOffset(0x1E0)]
        public Debug.DynamicArray<Debug.BasicString> m_AssemblyNames;

        [FieldOffset(0x208)]
        public Debug.DynamicArray<int> m_AssemblyTypes;

        [FieldOffset(0x230)]
        public Debug.DynamicArray<IntPtr> m_ScriptImages;

        [FieldOffset(0x260)]
        public Debug.DynamicArray<int> m_AssemblyMonoPathsIndex;

        [FieldOffset(0x298)]
        public byte m_AreAssembliesLoaded;

        // +0x2A0: core::hash_map<basic_string, ScriptingImagePtr> m_ScriptingImageCache (~40 bytes in dev)

        public bool AreAssembliesLoaded => m_AreAssembliesLoaded != 0;

        [UnscopedRef]
        public ref Debug.DynamicArray<Debug.BasicString> AssemblyNames => ref m_AssemblyNames;

        [UnscopedRef]
        public ref Debug.DynamicArray<int> AssemblyTypes => ref m_AssemblyTypes;

        [UnscopedRef]
        public ref Debug.DynamicArray<IntPtr> ScriptImages => ref m_ScriptImages;

        [UnscopedRef]
        public ref Debug.DynamicArray<int> AssemblyMonoPathsIndex => ref m_AssemblyMonoPathsIndex;
    }

    /// <summary>RVA of <c>GetMonoManager()</c> in UnityPlayer.dll (image base 0x180000000).</summary>
    public const long RvaGetMonoManager = 0x0116E8F0;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr GetMonoManagerDelegate();

    /// <summary>
    /// Strategy struct plugging the WinDbg variant into the generic
    /// SerializationFix.RegisterAssemblies pipeline.
    /// </summary>
    internal readonly struct Platform : IPlatform<Debug.BasicString>
    {
        public Debug.BasicString CreateString(IntPtr data, int size) => new(data, size);

        public IntPtr GetMonoManagerPointer()
        {
            IntPtr fnPtr = Windows.GetUnityPlayerFunctionPointer(RvaGetMonoManager);
            if (fnPtr == IntPtr.Zero)
                return IntPtr.Zero;
            var fn = Marshal.GetDelegateForFunctionPointer<GetMonoManagerDelegate>(fnPtr);
            return fn();
        }
    }

    internal static void RegisterAssemblies(AssemblyInfo[] infos) =>
        SerializationFix.RegisterAssemblies<
            Platform,
            Debug.BasicString,
            Debug.DynamicArray<Debug.BasicString>,
            Debug.DynamicArray<int>,
            Debug.DynamicArray<IntPtr>,
            MonoManager
        >(default, infos);
}
