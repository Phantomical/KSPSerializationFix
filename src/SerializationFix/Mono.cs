// Helpers for crossing from a managed System.Reflection.Assembly to the native
// MonoImage* that backs it. Two steps:
//
//   1. Every managed Assembly under Mono stores its native MonoAssembly* in an
//      IntPtr field named "_mono_assembly" on System.Reflection.Assembly.
//   2. mono_assembly_get_image(MonoAssembly*) returns the MonoImage*. This is
//      a public Mono C API exported by the mono runtime library that Unity
//      loaded into the process.
//
// The mono library Unity ships has different basenames on different platforms:
//   - Windows: MonoBleedingEdge\EmbedRuntime\mono-2.0-bdwgc.dll
//   - Linux:   Data/MonoBleedingEdge/x86_64/libmonobdwgc-2.0.so
//   - macOS:   .../Contents/Frameworks/libmonobdwgc-2.0.dylib
//
// This shakes out to a unix binding and a windows binding for the function.
// Since the library has already been loaded then it will be resolved appropriately,
// even though we aren't shipping it ourselves.

using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace KSPSerializationFix;

internal static class Mono
{
    /// <summary>
    /// Returns the native <c>MonoImage*</c> backing <paramref name="assembly"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">If <paramref name="assembly"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// If the assembly has no backing MonoAssembly (e.g. dynamic assemblies).
    /// </exception>
    public static IntPtr GetMonoImage(Assembly assembly)
    {
        if (assembly == null)
            throw new ArgumentNullException(nameof(assembly));
        if (assembly._mono_assembly == IntPtr.Zero)
            throw new InvalidOperationException(
                $"Assembly {assembly.FullName} had a null _mono_assembly field"
            );

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return WindowsMono.mono_assembly_get_image(assembly._mono_assembly);
        return UnixMono.mono_assembly_get_image(assembly._mono_assembly);
    }

    // The DLLImport methods are in nested classes so we only try to load the
    // one that is actually needed for the current platform.

    private static class WindowsMono
    {
        [DllImport(
            "mono-2.0-bdwgc",
            EntryPoint = "mono_assembly_get_image",
            CallingConvention = CallingConvention.Cdecl
        )]
        public static extern IntPtr mono_assembly_get_image(IntPtr monoAssembly);
    }

    private static class UnixMono
    {
        [DllImport(
            "monobdwgc-2.0",
            EntryPoint = "mono_assembly_get_image",
            CallingConvention = CallingConvention.Cdecl
        )]
        public static extern IntPtr mono_assembly_get_image(IntPtr monoAssembly);
    }
}
