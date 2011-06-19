using System;

namespace BitMaker.Miner.Plugin.Cpu
{

    /// <summary>
    /// Provides various utilities.
    /// </summary>
    public static class BitUtil
    {

        /// <summary>
        /// Copies the data in <paramref name="src"/> to <paramref name="dst"/>.
        /// </summary>
        /// <param name="src"></param>
        /// <param name="dst"></param>
        /// <param name="ofs"></param>
        /// <param name="sz"></param>
        public static unsafe void Copy(void* src, void* dst, int ofs, int sz)
        {
            for (int i = 0; i < sz / IntPtr.Size; i++)
                ((IntPtr*)dst)[i] = ((IntPtr*)src)[i];
        }

        /// <summary>
        /// Flips the endianess of a uint.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static uint ReverseEndian(uint value)
        {
            return
                (value & 0x000000ffU) << 24 |
                (value & 0x0000ff00U) << 8 |
                (value & 0x00ff0000U) >> 8 |
                (value & 0xff000000U) >> 24;
        }

        /// <summary>
        /// Flips the endianness of a ulong.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static ulong ReverseEndian(ulong value)
        {
            return
                (value & 0x00000000000000ffU) << 56 |
                (value & 0x000000000000ff00U) << 40 |
                (value & 0x0000000000ff0000U) << 24 |
                (value & 0x00000000ff000000U) << 8 |
                (value & 0x000000ff00000000U) >> 8 |
                (value & 0x0000ff0000000000U) >> 24 |
                (value & 0x00ff000000000000U) >> 40 |
                (value & 0xff00000000000000U) >> 56;
        }

        /// <summary>
        /// Flips the endianess of every uint in a buffer.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="size"></param>
        public static unsafe void ReverseEndian(uint* buffer, int size)
        {
            for (int i = 0; i < size; i++)
                buffer[i] = ReverseEndian(buffer[i]);
        }

        /// <summary>
        /// Reverses the order of a buffer.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="size"></param>
        public static unsafe void ReverseBuffer(byte* buffer, int size)
        {
            byte* temp = stackalloc byte[size];
            for (int i = 0; i < size; i++)
                temp[size - i - 1] = buffer[i];

            Copy(temp, buffer, 0, size);
        }

        /// <summary>
        /// Reverse the order of a buffer.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="size"></param>
        public static unsafe void ReverseBuffer(uint* buffer, int size)
        {
            uint* temp = stackalloc uint[size];
            for (int i = 0; i < size; i++)
                temp[size - i - 1] = buffer[i];

            Copy(temp, buffer, 0, size);
        }

    }

}
