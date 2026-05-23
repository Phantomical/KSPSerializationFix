// Layouts shared by every development (debug) Unity 2019.4.18f1 player variant.
//
// Dev builds expand MemLabelId from 4 -> 12 bytes (AllocationRootWithSalt
// pointer + RootReferenceIndex + identifier). That pushes m_size/m_capacity
// 8 bytes deeper inside dynamic_array (32 -> 40) and grows basic_string from
// 40 -> 48 bytes. Every MonoManager field therefore sits 0x30+ later than its
// release counterpart on the matching platform.

using System;
using System.Runtime.InteropServices;

namespace KSPSerializationFix.Platform;

internal static class Debug
{
    /// <summary>dynamic_array&lt;T&gt; - 40 bytes (dev MemLabelId is 12 bytes).</summary>
    [StructLayout(LayoutKind.Sequential, Size = 40)]
    internal struct DynamicArray : IDynamicArrayOps
    {
        public IntPtr m_ptr; // +0x00
        public IntPtr m_RootReferenceWithSalt; // +0x08
        public int m_labelId; // +0x10
        public int m_labelPad; // +0x14
        public ulong m_size; // +0x18
        public ulong m_capacity; // +0x20

        public ulong Size => m_size;
        public IntPtr Ptr => m_ptr;

        /// <summary>
        /// Append a sequence of elements. Always reallocates a fresh buffer
        /// and memcpys the existing contents. The new buffer is marked
        /// borrowed (low bit of m_capacity = 1) so Unity's dtor at
        /// ~dynamic_array skips both per-element destruction and the buffer
        /// free. The previous m_ptr is intentionally leaked - Unity allocated
        /// it via a MemLabelId-aware allocator we cannot match from C#.
        ///
        /// m_capacity stores the raw count with the LSB as flag (verified
        /// against the move ctor at UnityPlayer.dll +0x134DB0 which copies
        /// it verbatim). We round the allocation up to (newSize | 1) so the
        /// 1-slot slack implied by an even newSize is real backing memory,
        /// in case any code path treats m_capacity literally and writes
        /// past m_size. Caller must ensure no other code is concurrently
        /// reading the array.
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
