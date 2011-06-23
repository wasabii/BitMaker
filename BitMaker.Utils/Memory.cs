using System;
using System.Text;

namespace BitMaker.Utils
{

    /// <summary>
    /// Provides various utilities.
    /// </summary>
    public static class Memory
    {

        public static unsafe void Zero(byte* dst, int sz)
        {
            for (int i = 0; i < sz; i++)
                dst[i] = 0x00;
        }

        /// <summary>
        /// Copies <paramref name="sz"/> integers from <paramref name="src"/> to <paramref name="dst"/>.
        /// </summary>
        /// <param name="src"></param>
        /// <param name="dst"></param>
        /// <param name="sz"></param>
        public static unsafe void Copy(uint[] src, uint* dst, int sz)
        {
            fixed (uint* srcPtr = src)
                Copy((byte*)srcPtr, (byte*)dst, sz * sizeof(uint));
        }

        /// <summary>
        /// Copies <paramref name="sz"/> integers from <paramref name="src"/> to <paramref name="dst"/>.
        /// </summary>
        /// <param name="src"></param>
        /// <param name="dst"></param>
        /// <param name="sz"></param>
        public static unsafe void Copy(uint* src, uint* dst, int sz)
        {
            Copy((byte*)src, (byte*)dst, sz * sizeof(uint));
        }

        /// <summary>
        /// Copies <paramref name="sz"/> elements from <paramref name="src"/> to <paramref name="dst"/>. <paramref
        /// name="sz"/> must be aligned with the platform's <see cref="IntPtr"/> size.
        /// </summary>
        /// <param name="src"></param>
        /// <param name="dst"></param>
        /// <param name="sz"></param>
        public static unsafe void Copy(byte* src, byte* dst, int sz)
        {
            for (int i = 0; i < sz / IntPtr.Size; i++)
                ((IntPtr*)dst)[i] = ((IntPtr*)src)[i];
            for (int i = 0; i < sz % IntPtr.Size; i++)
                dst[(sz / IntPtr.Size) * IntPtr.Size + i] = src[(sz / IntPtr.Size) * IntPtr.Size + i];
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
        public static unsafe void ReverseEndian(uint[] buffer)
        {
            fixed (uint* bufferPtr = buffer)
                ReverseEndian(bufferPtr, buffer.Length);
        }

        /// <summary>
        /// Flips the endianess of every uint in a buffer.
        /// </summary>
        /// <param name="buffer"></param>
        public static unsafe void ReverseEndian(uint[] buffer, uint[] output)
        {
            if (buffer.Length != output.Length)
                throw new ArgumentException();

            fixed (uint* bufferPtr = buffer, outputPtr = output)
                ReverseEndian(bufferPtr, outputPtr, buffer.Length);
        }

        /// <summary>
        /// Flips the endianess of every uint in a buffer.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="size"></param>
        public static unsafe void ReverseEndian(uint* buffer, int size)
        {
            ReverseEndian(buffer, buffer, size);
        }

        /// <summary>
        /// Flips the endianess of every uint in a buffer.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="size"></param>
        public static unsafe void ReverseEndian(uint* buffer, uint* output, int size)
        {
            for (int i = 0; i < size; i++)
                output[i] = ReverseEndian(buffer[i]);
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

            Copy(temp, buffer, size);
        }

        /// <summary>
        /// Decodes a hex string into a byte array.
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static byte[] Decode(string str)
        {
            if (str.Length % 8 != 0)
                throw new DataMisalignedException("Hex string must be multiple of 8 characters long.");

            var buf = new byte[str.Length / 2];
            for (int i = 0, j = 0; j < str.Length; i++, j += 2)
                buf[i] = Convert.ToByte(str.Substring(j, 2), 16);
            return buf;
        }

        /// <summary>
        /// Encodes a uint array into a hex string.
        /// </summary>
        /// <param name="buf"></param>
        /// <returns></returns>
        public static string Encode(uint[] buf)
        {
            var b = new StringBuilder(buf.Length * 8);
            for (int i = 0; i < buf.Length; i++)
                b.AppendFormat("{0:x8}", buf[i]);
            return b.ToString();
        }

        /// <summary>
        /// Encodes a byte array into a hex string.
        /// </summary>
        /// <param name="buf"></param>
        /// <returns></returns>
        public static string Encode(byte[] buf)
        {
            var b = new StringBuilder(buf.Length * 2);
            for (int i = 0; i < buf.Length; i++)
                b.AppendFormat("{0:x2}", buf[i]);
            return b.ToString();
        }

    }

}
