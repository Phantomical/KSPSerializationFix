// Linux release (non-development) Unity 2019.4.18f1 player layout.
// Variation: linux64_withgfx_nondevelopment_mono.
//
// Unity strips DWARF type info from Linux player builds; field offsets were
// recovered by Ghidra disassembly of MonoManager::BeginReloadAssembly and
// MonoManager::LoadAssemblies in UnityPlayer.so (build-id
// 837d925b00ed85912e7d3152aa0ad3bd6d9b4c2d) and cross-verified against the
// macOS non-dev dSYM DWARF, which matches exactly (Itanium ABI).
//
// MonoManager fields are 8 bytes earlier than WinRel because Itanium ABI
// elides the empty-base padding MSVC inserts. Embedded dynamic_array /
// basic_string layouts are shared across release variants - see Release.cs.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace KSPSerializationFix.Platform;

internal static class LinuxRel
{
    /// <summary>
    /// MonoManager - field offsets match MacRel exactly (Itanium ABI).
    /// sizeof not directly verified for Linux; 608 mirrors macOS.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 608)]
    public struct MonoManager
        : IMonoManager<
            Release.BasicString,
            Release.DynamicArray<Release.BasicString>,
            Release.DynamicArray<int>,
            Release.DynamicArray<IntPtr>
        >
    {
        [FieldOffset(0x1A8)]
        public Release.DynamicArray<Release.BasicString> m_AssemblyNames;

        [FieldOffset(0x1C8)]
        public Release.DynamicArray<int> m_AssemblyTypes;

        [FieldOffset(0x1E8)]
        public Release.DynamicArray<IntPtr> m_ScriptImages;

        [FieldOffset(0x210)]
        public Release.DynamicArray<int> m_AssemblyMonoPathsIndex;

        [FieldOffset(0x240)]
        public byte m_AreAssembliesLoaded;

        // +0x248: core::hash_map<basic_string, ScriptingImagePtr> m_ScriptingImageCache

        public bool AreAssembliesLoaded => m_AreAssembliesLoaded != 0;

        [UnscopedRef]
        public ref Release.DynamicArray<Release.BasicString> AssemblyNames => ref m_AssemblyNames;

        [UnscopedRef]
        public ref Release.DynamicArray<int> AssemblyTypes => ref m_AssemblyTypes;

        [UnscopedRef]
        public ref Release.DynamicArray<IntPtr> ScriptImages => ref m_ScriptImages;

        [UnscopedRef]
        public ref Release.DynamicArray<int> AssemblyMonoPathsIndex =>
            ref m_AssemblyMonoPathsIndex;
    }

    // Function / global RVAs in UnityPlayer.so (image base 0x0).
    public const long RvaGetMonoManager = 0x00AEABC0;
    public const long RvaGetManagerFromContext = 0x0078D310;
    public const long RvaGetManagerContext = 0x0078D350;
    public const long RvaIsManagerContextAvailable = 0x0078D2F0;
    public const long RvaGContext = 0x02060FC0;

    // MonoManager field offsets.
    public const int OffAssemblyNames = 0x1A8;
    public const int OffAssemblyTypes = 0x1C8;
    public const int OffScriptImages = 0x1E8;
    public const int OffAssemblyMonoPathsIndex = 0x210;
    public const int OffAreAssembliesLoaded = 0x240;
    public const int OffScriptingImageCache = 0x248;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr GetMonoManagerDelegate();

    /// <summary>
    /// Strategy struct plugging the LinuxRel variant into the generic
    /// SerializationFix.RegisterAssemblies pipeline.
    /// </summary>
    internal readonly struct Platform : IPlatform<Release.BasicString>
    {
        public Release.BasicString CreateString(IntPtr data, int size) => new(data, size);

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
            Release.BasicString,
            Release.DynamicArray<Release.BasicString>,
            Release.DynamicArray<int>,
            Release.DynamicArray<IntPtr>,
            MonoManager
        >(default, infos);
}
