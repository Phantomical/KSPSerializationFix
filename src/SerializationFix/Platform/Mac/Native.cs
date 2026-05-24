// Platform-level helpers for resolving native function pointers inside the
// currently loaded UnityPlayer.dylib on macOS. Variant-specific RVAs are
// declared in MacRel.cs / MacDbg.cs and passed in by the caller.
//
// Mach-O dylibs are ASLR-slid. We walk dyld's image list to find UnityPlayer
// and add its vmaddr-slide to our recorded RVA. The dylib's __TEXT segment
// starts at vmaddr 0, so RVA equals in-segment file offset and slide + RVA
// gives the runtime virtual address.

using System;
using System.Runtime.InteropServices;

namespace KSPSerializationFix.Platform.Mac;

internal static class Native
{
    /// <summary>Module basename matched against _dyld_get_image_name (suffix match).</summary>
    public const string UnityPlayerModule = "UnityPlayer.dylib";

    [DllImport("libSystem.dylib", EntryPoint = "_dyld_image_count")]
    private static extern uint dyld_image_count();

    [DllImport("libSystem.dylib", EntryPoint = "_dyld_get_image_name")]
    private static extern IntPtr dyld_get_image_name(uint i);

    [DllImport("libSystem.dylib", EntryPoint = "_dyld_get_image_vmaddr_slide")]
    private static extern IntPtr dyld_get_image_vmaddr_slide(uint i);

    /// <summary>
    /// Returns a native function pointer at <paramref name="rva"/> inside
    /// UnityPlayer.dylib. <see cref="IntPtr.Zero"/> if the dylib is not
    /// present in the dyld image list.
    /// </summary>
    public static IntPtr GetUnityPlayerFunctionPointer(long rva)
    {
        uint n = dyld_image_count();
        for (uint i = 0; i < n; i++)
        {
            IntPtr namePtr = dyld_get_image_name(i);
            string name = Marshal.PtrToStringAnsi(namePtr);
            if (name != null && name.EndsWith(UnityPlayerModule, StringComparison.Ordinal))
            {
                IntPtr slide = dyld_get_image_vmaddr_slide(i);
                return new IntPtr(slide.ToInt64() + rva);
            }
        }
        return IntPtr.Zero;
    }
}
