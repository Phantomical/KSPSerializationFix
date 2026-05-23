// Windows release (non-development) Unity 2019.4.18f1 player layout.
// Verified from UnityPlayer_Win64_mono_x64.pdb
// (GUID 41E8D80F-828B-4CEC-840B-0E4C0496BCCF, age 1).
//
// MSVC ABI: 8-byte empty-base padding inflates MonoManager vs Itanium ABI.
// Embedded dynamic_array / basic_string layouts are shared across release
// variants - see Release.cs.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace KSPSerializationFix.Platform;

internal static class WinRel
{
    /// <summary>
    /// MonoManager - sizeof 624 (verified from PDB). Only the fields the
    /// serialization-fix patcher touches are mapped; remaining bytes are opaque.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 624)]
    internal struct MonoManager : IMonoManager<Release.DynamicArray>
    {
        [FieldOffset(0x1B0)]
        public Release.DynamicArray m_AssemblyNames;

        [FieldOffset(0x1D0)]
        public Release.DynamicArray m_AssemblyTypes;

        [FieldOffset(0x1F0)]
        public Release.DynamicArray m_ScriptImages;

        [FieldOffset(0x218)]
        public Release.DynamicArray m_AssemblyMonoPathsIndex;

        [FieldOffset(0x248)]
        public byte m_AreAssembliesLoaded;

        // +0x250: core::hash_map<basic_string, ScriptingImagePtr> m_ScriptingImageCache (~32 bytes)

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

    // Function / global RVAs in UnityPlayer.dll (image base 0x180000000).
    public const long RvaGetMonoManager = 0x008B13A0;
    public const long RvaGetManagerFromContext = 0x0058E190;
    public const long RvaGetManagerContext = 0x0058E180;
    public const long RvaIsManagerContextAvailable = 0x0058E5A0;
    public const long RvaGContext = 0x017CD640;

    // MonoManager field offsets (also exposed as plain constants for non-struct callers).
    public const int OffAssemblyNames = 0x1B0;
    public const int OffAssemblyTypes = 0x1D0;
    public const int OffScriptImages = 0x1F0;
    public const int OffAssemblyMonoPathsIndex = 0x218;
    public const int OffAreAssembliesLoaded = 0x248;
    public const int OffScriptingImageCache = 0x250;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr GetMonoManagerDelegate();

    /// <summary>
    /// Strategy struct plugging the WinRel variant into the generic
    /// SerializationFix.RegisterAssemblies pipeline.
    /// </summary>
    internal readonly struct Platform : IPlatform<Release.BasicString>
    {
        public Release.BasicString CreateString(IntPtr data, int size) => new(data, size);

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
            Release.BasicString,
            Release.DynamicArray,
            MonoManager
        >(default, infos);
}
