// Linux debug (development) Unity 2019.4.18f1 player layout.
// Variation: linux64_withgfx_development_mono.
//
// Field offsets recovered by Ghidra disassembly of MonoManager::BeginReloadAssembly
// and MonoManager::LoadAssemblies; layout matches MacDbg exactly (Itanium ABI
// + dev MemLabelId expansion). Embedded dynamic_array / basic_string layouts
// are shared across dev variants - see Debug.cs.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace KSPSerializationFix.Platform;

internal static class LinuxDbg
{
    /// <summary>
    /// MonoManager - field offsets match MacDbg exactly. sizeof not directly
    /// verified; 696 extrapolated from WinDbg's hash_map size delta.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 696)]
    public struct MonoManager
        : IMonoManager<
            Debug.BasicString,
            Debug.DynamicArray<Debug.BasicString>,
            Debug.DynamicArray<int>,
            Debug.DynamicArray<IntPtr>
        >
    {
        [FieldOffset(0x1D8)]
        public Debug.DynamicArray<Debug.BasicString> m_AssemblyNames;

        [FieldOffset(0x200)]
        public Debug.DynamicArray<int> m_AssemblyTypes;

        [FieldOffset(0x228)]
        public Debug.DynamicArray<IntPtr> m_ScriptImages;

        [FieldOffset(0x258)]
        public Debug.DynamicArray<int> m_AssemblyMonoPathsIndex;

        [FieldOffset(0x290)]
        public byte m_AreAssembliesLoaded;

        // +0x298: core::hash_map<basic_string, ScriptingImagePtr> m_ScriptingImageCache

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

    // Function / global RVAs in UnityPlayer.so (image base 0x0).
    public const long RvaGetMonoManager = 0x01378BA0;
    public const long RvaGetManagerFromContext = 0x00B88E70;
    public const long RvaGetManagerContext = 0x00B88EB0;
    public const long RvaIsManagerContextAvailable = 0x00B88E50;
    public const long RvaGContext = 0x02E89800;

    // MonoManager field offsets.
    public const int OffAssemblyNames = 0x1D8;
    public const int OffAssemblyTypes = 0x200;
    public const int OffScriptImages = 0x228;
    public const int OffAssemblyMonoPathsIndex = 0x258;
    public const int OffAreAssembliesLoaded = 0x290;
    public const int OffScriptingImageCache = 0x298;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr GetMonoManagerDelegate();

    /// <summary>
    /// Strategy struct plugging the LinuxDbg variant into the generic
    /// SerializationFix.RegisterAssemblies pipeline.
    /// </summary>
    internal readonly struct Platform : IPlatform<Debug.BasicString>
    {
        public Debug.BasicString CreateString(IntPtr data, int size) => new(data, size);

        public IntPtr GetMonoManagerPointer()
        {
            IntPtr fnPtr = Linux.GetUnityPlayerFunctionPointer(RvaGetMonoManager);
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
