// Platform-level helpers for resolving native function pointers inside the
// currently loaded UnityPlayer.so on Linux. Variant-specific RVAs are declared
// in LinuxRel.cs / LinuxDbg.cs and passed in by the caller.
//
// UnityPlayer.so isn't loaded by us — we just need its mapped base address.
// dl_iterate_phdr (glibc) walks the dynamic linker's link map; the dlpi_addr
// field is the runtime load base. The function pointer is then base + RVA.

using System;
using System.Runtime.InteropServices;

namespace KSPSerializationFix.Platform;

internal static class Linux
{
    /// <summary>Module basename matched against dl_iterate_phdr's dlpi_name.</summary>
    public const string UnityPlayerModule = "UnityPlayer.so";

    [DllImport("libdl.so.2", EntryPoint = "dl_iterate_phdr")]
    private static extern int dl_iterate_phdr(IntPtr callback, IntPtr data);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int PhdrCallback(IntPtr info, UIntPtr size, IntPtr data);

    /// <summary>
    /// struct dl_phdr_info (64-bit glibc) — we only need the first two fields:
    ///   +0x00  ElfW(Addr)        dlpi_addr   (runtime load base)
    ///   +0x08  const char       *dlpi_name   (path or basename)
    ///   ...
    /// </summary>
    private static int OnPhdr(IntPtr info, UIntPtr size, IntPtr data)
    {
        IntPtr namePtr = Marshal.ReadIntPtr(info, IntPtr.Size);
        string name = Marshal.PtrToStringAnsi(namePtr);
        if (name != null && name.Contains(UnityPlayerModule))
        {
            IntPtr loadAddr = Marshal.ReadIntPtr(info, 0);
            Marshal.WriteIntPtr(data, loadAddr);
            return 1; // stop iteration
        }
        return 0;
    }

    /// <summary>
    /// Returns a native function pointer at <paramref name="rva"/> inside
    /// UnityPlayer.so. <see cref="IntPtr.Zero"/> if the module is not in the
    /// process's link map.
    /// </summary>
    public static IntPtr GetUnityPlayerFunctionPointer(long rva)
    {
        IntPtr result = Marshal.AllocHGlobal(IntPtr.Size);
        Marshal.WriteIntPtr(result, IntPtr.Zero);
        // Keep the delegate alive across the native call so its function pointer
        // isn't yanked from under dl_iterate_phdr by the GC.
        PhdrCallback cb = OnPhdr;
        try
        {
            IntPtr cbPtr = Marshal.GetFunctionPointerForDelegate(cb);
            dl_iterate_phdr(cbPtr, result);
            IntPtr loadAddr = Marshal.ReadIntPtr(result);
            if (loadAddr == IntPtr.Zero)
                return IntPtr.Zero;
            return new IntPtr(loadAddr.ToInt64() + rva);
        }
        finally
        {
            Marshal.FreeHGlobal(result);
            GC.KeepAlive(cb);
        }
    }
}
