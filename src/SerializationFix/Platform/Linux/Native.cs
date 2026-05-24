// Platform-level helpers for resolving native function pointers on Linux.
// Two lookups, both rooted in dl_iterate_phdr (glibc), which walks the dynamic
// linker's link map.
//
//   - UnityPlayer.so: we just need its runtime load base. The dlpi_addr field
//     gives that; callers add their RVA. Variant-specific RVAs are declared in
//     LinuxRel.cs / LinuxDbg.cs and passed in.
//   - libmonobdwgc-2.0.so: we can't reach it via [DllImport]. "__Internal"
//     fails because Mono caches a dlopen(NULL) handle and resolves symbols
//     against it via dlsym; in practice that lookup does not see the mono
//     runtime library on KSP's Unity 2019.4.18f1 build. Even after promoting
//     libmonobdwgc-2.0.so with RTLD_NOLOAD | RTLD_GLOBAL (which gdb confirms
//     sets the flag) Mono's __Internal dlsym still misses the symbol —
//     consistent with glibc behavior where RTLD_GLOBAL promotion doesn't add
//     the lib to the main link map's global search scope (cf. glibc bug
//     16634). Naming the library by basename also fails because Unity loads it
//     from Data/MonoBleedingEdge/x86_64/, which isn't on the dynamic linker's
//     search path.
//
//     GetMonoFunctionPointer walks dl_iterate_phdr for the full path Unity
//     loaded the library from, dlopens with RTLD_NOLOAD to grab a handle to
//     that exact image, and dlsyms the requested symbol. The caller wraps the
//     resulting function pointer in a delegate.

using System;
using System.Runtime.InteropServices;

namespace KSPSerializationFix.Platform.Linux;

internal static class Native
{
    /// <summary>Module basename matched against dl_iterate_phdr's dlpi_name.</summary>
    public const string UnityPlayerModule = "UnityPlayer.so";

    /// <summary>Mono library basename matched against dl_iterate_phdr's dlpi_name.</summary>
    public const string MonoLibraryModule = "libmonobdwgc-2.0";

    private const int RTLD_LAZY = 0x1;
    private const int RTLD_NOLOAD = 0x4;

    [DllImport("libdl.so.2", EntryPoint = "dl_iterate_phdr")]
    private static extern int dl_iterate_phdr(IntPtr callback, IntPtr data);

    [DllImport("libdl.so.2", EntryPoint = "dlopen")]
    private static extern IntPtr dlopen(string filename, int flags);

    [DllImport("libdl.so.2", EntryPoint = "dlsym")]
    private static extern IntPtr dlsym(IntPtr handle, string symbol);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int PhdrCallback(IntPtr info, UIntPtr size, IntPtr data);

    /// <summary>
    /// struct dl_phdr_info (64-bit glibc) — we only need the first two fields:
    ///   +0x00  ElfW(Addr)        dlpi_addr   (runtime load base)
    ///   +0x08  const char       *dlpi_name   (path or basename)
    ///   ...
    /// </summary>
    private static int OnPhdrUnityPlayer(IntPtr info, UIntPtr size, IntPtr data)
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

    private static int OnPhdrMonoLibrary(IntPtr info, UIntPtr size, IntPtr data)
    {
        IntPtr namePtr = Marshal.ReadIntPtr(info, IntPtr.Size);
        string name = Marshal.PtrToStringAnsi(namePtr);
        if (name != null && name.Contains(MonoLibraryModule))
        {
            // Hand the path back via a fresh unmanaged copy; caller owns it.
            Marshal.WriteIntPtr(data, Marshal.StringToHGlobalAnsi(name));
            return 1;
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
        PhdrCallback cb = OnPhdrUnityPlayer;
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

    /// <summary>
    /// Returns a native function pointer to <paramref name="symbol"/> exported
    /// from the loaded mono runtime library (libmonobdwgc-2.0.so).
    /// </summary>
    /// <exception cref="DllNotFoundException">
    /// If the mono library is not in the link map or its handle cannot be
    /// obtained.
    /// </exception>
    /// <exception cref="EntryPointNotFoundException">
    /// If <paramref name="symbol"/> is not exported from the mono library.
    /// </exception>
    public static IntPtr GetMonoFunctionPointer(string symbol)
    {
        string path =
            FindMonoLibraryPath()
            ?? throw new DllNotFoundException(
                $"Could not find {MonoLibraryModule} in the process's link map"
            );

        IntPtr handle = dlopen(path, RTLD_LAZY | RTLD_NOLOAD);
        if (handle == IntPtr.Zero)
            throw new DllNotFoundException($"dlopen({path}, RTLD_NOLOAD) returned NULL");

        IntPtr sym = dlsym(handle, symbol);
        if (sym == IntPtr.Zero)
            throw new EntryPointNotFoundException($"{symbol} not found in {path}");

        return sym;
    }

    private static string FindMonoLibraryPath()
    {
        IntPtr slot = Marshal.AllocHGlobal(IntPtr.Size);
        Marshal.WriteIntPtr(slot, IntPtr.Zero);
        PhdrCallback cb = OnPhdrMonoLibrary;
        try
        {
            dl_iterate_phdr(Marshal.GetFunctionPointerForDelegate(cb), slot);
            IntPtr pathPtr = Marshal.ReadIntPtr(slot);
            if (pathPtr == IntPtr.Zero)
                return null;
            try
            {
                return Marshal.PtrToStringAnsi(pathPtr);
            }
            finally
            {
                Marshal.FreeHGlobal(pathPtr);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(slot);
            GC.KeepAlive(cb);
        }
    }
}
