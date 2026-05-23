// macOS debug (development) Unity 2019.4.18f1 player layout.
// Variation: macosx64_development_mono.
//
// Unity does not ship a dSYM for the dev variant, so field offsets were
// recovered by Ghidra disassembly of MonoManager::BeginReloadAssembly and
// MonoManager::LoadAssemblies. Layout matches LinuxDbg exactly.
//
// sizeof(MonoManager) is not directly verified; 696 is extrapolated from
// WinDbg's hash_map size delta over its non-dev counterpart. Embedded
// dynamic_array / basic_string layouts are shared across dev variants -
// see Debug.cs.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace KSPSerializationFix.Platform;

internal static class MacDbg
{
    /// <summary>MonoManager - sizeof 696 (approximate).</summary>
    [StructLayout(LayoutKind.Explicit, Size = 696)]
    public struct MonoManager : IMonoManager<Debug.DynamicArray>
    {
        [FieldOffset(0x1D8)]
        public Debug.DynamicArray m_AssemblyNames;

        [FieldOffset(0x200)]
        public Debug.DynamicArray m_AssemblyTypes;

        [FieldOffset(0x228)]
        public Debug.DynamicArray m_ScriptImages;

        [FieldOffset(0x258)]
        public Debug.DynamicArray m_AssemblyMonoPathsIndex;

        [FieldOffset(0x290)]
        public byte m_AreAssembliesLoaded;

        // +0x298: core::hash_map<basic_string, ScriptingImagePtr> m_ScriptingImageCache (~32 bytes in dev)

        public bool AreAssembliesLoaded => m_AreAssembliesLoaded != 0;

        [UnscopedRef]
        public ref Debug.DynamicArray AssemblyNames => ref m_AssemblyNames;

        [UnscopedRef]
        public ref Debug.DynamicArray AssemblyTypes => ref m_AssemblyTypes;

        [UnscopedRef]
        public ref Debug.DynamicArray ScriptImages => ref m_ScriptImages;

        [UnscopedRef]
        public ref Debug.DynamicArray AssemblyMonoPathsIndex => ref m_AssemblyMonoPathsIndex;
    }

    // Function / global RVAs in UnityPlayer.dylib (image base 0x0).
    public const long RvaGetMonoManager = 0x0107D590;
    public const long RvaGetManagerFromContext = 0x00651290;
    public const long RvaGetManagerContext = 0x00651310;
    public const long RvaIsManagerContextAvailable = 0x00651270;
    public const long RvaGContext = 0x025AD918;

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
    /// Strategy struct plugging the MacDbg variant into the generic
    /// SerializationFix.RegisterAssemblies pipeline.
    /// </summary>
    internal readonly struct Platform : IPlatform<Debug.BasicString>
    {
        public Debug.BasicString CreateString(IntPtr data, int size) => new(data, size);

        public IntPtr GetMonoManagerPointer()
        {
            IntPtr fnPtr = Mac.GetUnityPlayerFunctionPointer(RvaGetMonoManager);
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
            Debug.DynamicArray,
            MonoManager
        >(default, infos);
}
