// Platform-level helpers for resolving native function pointers inside the
// currently loaded UnityPlayer.dll on Windows. Variant-specific RVAs are
// declared in WinRel.cs / WinDbg.cs and passed in by the caller.

using System;
using System.Runtime.InteropServices;

namespace KSPSerializationFix.Platform;

internal static class Windows
{
    /// <summary>Module basename for the running UnityPlayer in this process.</summary>
    public const string UnityPlayerModule = "UnityPlayer.dll";

    [DllImport(
        "kernel32.dll",
        CharSet = CharSet.Unicode,
        ExactSpelling = true,
        SetLastError = true
    )]
    private static extern IntPtr GetModuleHandleW(string lpModuleName);

    /// <summary>
    /// Returns a native function pointer at <paramref name="rva"/> inside
    /// UnityPlayer.dll. <see cref="IntPtr.Zero"/> if the module is not loaded.
    /// </summary>
    public static IntPtr GetUnityPlayerFunctionPointer(long rva)
    {
        IntPtr modBase = GetModuleHandleW(UnityPlayerModule);
        if (modBase == IntPtr.Zero)
            return IntPtr.Zero;
        return new IntPtr(modBase.ToInt64() + rva);
    }
}
