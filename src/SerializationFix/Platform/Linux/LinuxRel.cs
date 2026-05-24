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

namespace KSPSerializationFix.Platform.Linux;

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
        public ref Release.DynamicArray<int> AssemblyMonoPathsIndex => ref m_AssemblyMonoPathsIndex;
    }

    /// <summary>RVA of <c>GetMonoManager()</c> in UnityPlayer.so (image base 0x0).</summary>
    public const long RvaGetMonoManager = 0x00AEABC0;

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
            IntPtr fnPtr = Native.GetUnityPlayerFunctionPointer(RvaGetMonoManager);
            if (fnPtr == IntPtr.Zero)
                return IntPtr.Zero;
            var fn = Marshal.GetDelegateForFunctionPointer<GetMonoManagerDelegate>(fnPtr);
            return fn();
        }
    }

    internal static void RegisterAssemblies(System.Reflection.Assembly[] assemblies) =>
        SerializationFix.RegisterAssemblies<
            Platform,
            Release.BasicString,
            Release.DynamicArray<Release.BasicString>,
            Release.DynamicArray<int>,
            Release.DynamicArray<IntPtr>,
            MonoManager
        >(default, assemblies);
}
