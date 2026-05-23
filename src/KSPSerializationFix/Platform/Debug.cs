// Layouts shared by every development (debug) Unity 2019.4.18f1 player variant.
//
// Dev builds expand MemLabelId from 4 -> 12 bytes (AllocationRootWithSalt
// pointer + RootReferenceIndex + identifier). That pushes m_size/m_capacity
// 8 bytes deeper inside dynamic_array (32 -> 40) and grows basic_string from
// 40 -> 48 bytes. Every MonoManager field therefore sits 0x30+ later than its
// release counterpart on the matching platform.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace KSPSerializationFix.Platform;

internal static class Debug
{
    /// <summary>dynamic_array&lt;T&gt; - 40 bytes (dev MemLabelId is 12 bytes).</summary>
    [StructLayout(LayoutKind.Sequential, Size = 40)]
    internal struct DynamicArray<T> : IDynamicArray<T>
        where T : unmanaged
    {
        public IntPtr m_ptr; // +0x00
        public IntPtr m_RootReferenceWithSalt; // +0x08
        public int m_labelId; // +0x10
        public int m_labelPad; // +0x14
        public ulong m_size; // +0x18
        public ulong m_capacity; // +0x20

        [UnscopedRef]
        public ref IntPtr Ptr => ref m_ptr;

        [UnscopedRef]
        public ref ulong Size => ref m_size;

        [UnscopedRef]
        public ref ulong Capacity => ref m_capacity;
    }

    /// <summary>
    /// basic_string - 48 bytes. Same SSO layout as Release, larger MemLabelId trailer.
    ///
    /// Three-state discriminator (verified against StringStorageDefault dtor at
    /// UnityPlayer.dll +0x1045F0):
    ///   m_data == NULL                       -> SSO,    16 inline bytes at +0x08
    ///   m_data != NULL &amp;&amp; m_capacity != 0  -> owned,  destructor frees m_data
    ///   m_data != NULL &amp;&amp; m_capacity == 0  -> borrowed, destructor leaves m_data alone
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct BasicString
    {
        [FieldOffset(0x00)]
        public IntPtr m_data;

        [FieldOffset(0x08)]
        public ulong m_capacity;

        [FieldOffset(0x08)]
        public ulong m_ssoLo;

        [FieldOffset(0x10)]
        public ulong m_ssoHi;

        [FieldOffset(0x18)]
        public ulong m_size;

        [FieldOffset(0x20)]
        public IntPtr m_RootReferenceWithSalt;

        [FieldOffset(0x28)]
        public int m_labelId;

        [FieldOffset(0x2C)]
        public int m_labelPad;

        /// <summary>
        /// Constructs a borrowed (non-owning) basic_string that points at the
        /// caller-supplied buffer. Unity's destructor short-circuits on
        /// m_capacity == 0 and will never free <paramref name="data"/>.
        ///
        /// The buffer must remain valid for as long as Unity may dereference
        /// the string. For the assembly-registration use case the buffer lives
        /// for the player's lifetime and is intentionally leaked.
        /// </summary>
        public BasicString(IntPtr data, int size)
            : this()
        {
            m_data = data;
            m_capacity = 0;
            m_size = (ulong)size;
            m_labelId = FallbackStringLabel;
        }
    }

    // dynamic_array<T> internal layout (dev)
    public const int DynArraySizeof = 40;
    public const int DynArrayPtrOff = 0x00;
    public const int DynArrayLabelOff = 0x10;
    public const int DynArraySizeOff = 0x18;
    public const int DynArrayCapOff = 0x20;

    // basic_string internal layout (dev)
    public const int BasicStringSizeof = 48;
    public const int BasicStringDataOff = 0x00;
    public const int BasicStringCapOff = 0x08;
    public const int BasicStringSizeOff = 0x18;
    public const int BasicStringLabelOff = 0x28;

    /// <summary>Default MemLabelId fallback (kMemString) if no existing string slot can be sampled.</summary>
    public const int FallbackStringLabel = 0x48;
}
