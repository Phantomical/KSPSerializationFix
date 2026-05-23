// Layouts shared by every non-development (release) Unity 2019.4.18f1 player
// variant. In release builds MemLabelId is a single 4-byte int, so
// dynamic_array is 32 bytes and basic_string is 40 bytes.
//
// Layout matches across Windows / Linux / macOS in release: the only ABI-driven
// differences between platforms live in MonoManager's field offsets, not in
// these embedded structs.

using System;
using System.Runtime.InteropServices;

namespace KSPSerializationFix.Platform;

internal static class Release
{
    /// <summary>dynamic_array&lt;T&gt; - 32 bytes (release MemLabelId is 4 bytes).</summary>
    [StructLayout(LayoutKind.Sequential, Size = 32)]
    internal struct DynamicArray : IDynamicArrayOps
    {
        public IntPtr m_ptr; // +0x00
        public int m_labelId; // +0x08
        public int m_labelPad; // +0x0C (align to 8)
        public ulong m_size; // +0x10
        public ulong m_capacity; // +0x18

        public ulong Size => m_size;
        public IntPtr Ptr => m_ptr;

        /// <summary>
        /// Append a sequence of <typeparamref name="T"/> to the buffer.
        /// We always reallocate, and then mark the resulting buffer as being
        /// a borrowed buffer so unity never attempts to free it.
        /// </summary>
        public unsafe void Append<T>(T[] values)
            where T : unmanaged
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            if (values.Length == 0)
                return;

            int elemSize = sizeof(T);
            ulong addCount = (ulong)values.Length;
            ulong newSize = m_size + addCount;
            // Unity's dynamic_array treats a capacity with a 1 LSB as being
            // borrowed, so we set that allow it to be borrowed.
            ulong newCapacity = newSize | 1UL;
            long bytes = checked((long)newCapacity * elemSize);
            IntPtr newBuf = Marshal.AllocHGlobal((IntPtr)bytes);

            if (m_ptr != IntPtr.Zero && m_size > 0)
            {
                Buffer.MemoryCopy((void*)m_ptr, (void*)newBuf, bytes, (long)m_size * elemSize);
            }

            fixed (T* src = values)
            {
                Buffer.MemoryCopy(
                    src,
                    (byte*)newBuf + (long)m_size * elemSize,
                    bytes - (long)m_size * elemSize,
                    (long)addCount * elemSize
                );
            }

            m_ptr = newBuf;
            m_size = newSize;
            m_capacity = newCapacity;
        }
    }

    /// <summary>
    /// basic_string&lt;char, StringStorageDefault&lt;char&gt;&gt; - 40 bytes.
    ///
    /// Three-state discriminator (verified against StringStorageDefault dtor at
    /// UnityPlayer.dll +0x1045F0):
    ///   m_data == NULL                       -> SSO,    16 inline bytes at +0x08
    ///   m_data != NULL &amp;&amp; m_capacity != 0  -> owned,  destructor frees m_data
    ///   m_data != NULL &amp;&amp; m_capacity == 0  -> borrowed, destructor leaves m_data alone
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    public struct BasicString
    {
        [FieldOffset(0x00)]
        public IntPtr m_data;

        [FieldOffset(0x08)]
        public ulong m_capacity; // heap mode

        [FieldOffset(0x08)]
        public ulong m_ssoLo; // SSO bytes 0..7

        [FieldOffset(0x10)]
        public ulong m_ssoHi; // SSO bytes 8..15

        [FieldOffset(0x18)]
        public ulong m_size;

        [FieldOffset(0x20)]
        public int m_labelId;

        [FieldOffset(0x24)]
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

    // dynamic_array<T> internal layout
    public const int DynArraySizeof = 32;
    public const int DynArrayPtrOff = 0x00;
    public const int DynArrayLabelOff = 0x08;
    public const int DynArraySizeOff = 0x10;
    public const int DynArrayCapOff = 0x18;

    // basic_string internal layout
    public const int BasicStringSizeof = 40;
    public const int BasicStringDataOff = 0x00;
    public const int BasicStringCapOff = 0x08; // heap mode (SSO buffer overlaps)
    public const int BasicStringSizeOff = 0x18;
    public const int BasicStringLabelOff = 0x20;

    /// <summary>Default MemLabelId fallback (kMemString) if no existing string slot can be sampled.</summary>
    public const int FallbackStringLabel = 0x48;
}
