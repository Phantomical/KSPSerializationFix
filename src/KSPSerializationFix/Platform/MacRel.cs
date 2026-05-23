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

        // +0x248: core::hash_map<basic_string, ScriptingImagePtr> m_ScriptingImageCache (~24 bytes)

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

    /// <summary>RVA of <c>GetMonoManager()</c> in UnityPlayer.dylib (image base 0x0).</summary>
    public const long RvaGetMonoManager = 0x0087E0A0;

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
