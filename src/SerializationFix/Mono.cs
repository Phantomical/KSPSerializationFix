// Helpers for crossing from a managed System.Reflection.Assembly to the native
// MonoImage* that backs it. Two steps:
//
//   1. Every managed Assembly under Mono stores its native MonoAssembly* in an
//      IntPtr field named "_mono_assembly" on System.Reflection.Assembly.
//   2. mono_assembly_get_image(MonoAssembly*) returns the MonoImage*. This is
//      a public Mono C API exported by the mono runtime library that Unity
//      loaded into the process.
//
// How we reach mono_assembly_get_image varies by platform; see the per-class
// comments below. Each binding lives in its own nested class so we only ever
// try to resolve the symbol from the source that matches the current platform.

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

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return LinuxMono.mono_assembly_get_image(assembly._mono_assembly);

        return InternalMono.mono_assembly_get_image(assembly._mono_assembly);
    }

    // On Windows and MacOS we can just use __Internal to resolve the symbol
    // against the loaded libraries in the current process.
    private static class InternalMono
    {
        [DllImport(
            "__Internal",
            EntryPoint = "mono_assembly_get_image",
            CallingConvention = CallingConvention.Cdecl
        )]
        public static extern IntPtr mono_assembly_get_image(IntPtr monoAssembly);
    }

    // __Internal doesn't seem to work on linux, so we manually get a handle to
    // libmono and call dlsym to get the function pointer ourselves.
    private static class LinuxMono
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr MonoAssemblyGetImage(IntPtr monoAssembly);

        private static readonly MonoAssemblyGetImage _fn = Resolve();

        public static IntPtr mono_assembly_get_image(IntPtr monoAssembly) => _fn(monoAssembly);

        private static MonoAssemblyGetImage Resolve()
        {
            IntPtr sym = Platform.Linux.Native.GetMonoFunctionPointer("mono_assembly_get_image");
            return Marshal.GetDelegateForFunctionPointer<MonoAssemblyGetImage>(sym);
        }
    }
}
