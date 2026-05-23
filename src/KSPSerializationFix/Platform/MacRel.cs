// macOS release (non-development) Unity 2019.4.18f1 player layout.
// Variation: macosx64_nondevelopment_mono.
//
// Verified from UnityPlayer.dSYM DWARF. Itanium ABI -> MonoManager fields sit
// 8 bytes earlier than WinRel. Linux non-dev has the identical layout.
// Embedded dynamic_array / basic_string layouts are shared across release
// variants - see Release.cs.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace KSPSerializationFix.Platform;

internal static class MacRel
{
    /// <summary>MonoManager - sizeof 608 (verified from dSYM DWARF).</summary>
    [StructLayout(LayoutKind.Explicit, Size = 608)]
    public struct MonoManager : IMonoManager<Release.DynamicArray>
    {
        [FieldOffset(0x1A8)]
        public Release.DynamicArray m_AssemblyNames;

        [FieldOffset(0x1C8)]
        public Release.DynamicArray m_AssemblyTypes;

        [FieldOffset(0x1E8)]
        public Release.DynamicArray m_ScriptImages;

        [FieldOffset(0x210)]
        public Release.DynamicArray m_AssemblyMonoPathsIndex;

        [FieldOffset(0x240)]
        public byte m_AreAssembliesLoaded;

        // +0x248: core::hash_map<basic_string, ScriptingImagePtr> m_ScriptingImageCache (~24 bytes)

        public bool AreAssembliesLoaded => m_AreAssembliesLoaded != 0;

        [UnscopedRef]
        public ref Release.DynamicArray AssemblyNames => ref m_AssemblyNames;

        [UnscopedRef]
        public ref Release.DynamicArray AssemblyTypes => ref m_AssemblyTypes;

        [UnscopedRef]
        public ref Release.DynamicArray ScriptImages => ref m_ScriptImages;

        [UnscopedRef]
        public ref Release.DynamicArray AssemblyMonoPathsIndex => ref m_AssemblyMonoPathsIndex;
    }

    // Function / global RVAs in UnityPlayer.dylib (image base 0x0).
    public const long RvaGetMonoManager = 0x0087E0A0;
    public const long RvaGetManagerFromContext = 0x004C9CE0;
    public const long RvaGetManagerContext = 0x004C9D60;
    public const long RvaIsManagerContextAvailable = 0x004C9CC0;
    public const long RvaGContext = 0x0189A990;

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
    /// Strategy struct plugging the MacRel variant into the generic
    /// SerializationFix.RegisterAssemblies pipeline.
    /// </summary>
    internal readonly struct Platform : IPlatform<Release.BasicString>
    {
        public Release.BasicString CreateString(IntPtr data, int size) => new(data, size);

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
            Release.BasicString,
            Release.DynamicArray,
            MonoManager
        >(default, infos);
}
