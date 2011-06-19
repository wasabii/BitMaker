using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BitMaker.Miner
{

    internal static class Utils
    {

        /// <summary>
        /// Decodes a hex string into a byte array.
        /// </summary>
        /// <param name="str"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        public static byte[] DecodeHex(string str, int size)
        {
            var buf = new byte[size];
            for (int i = 0, j = 0; i < size; i++, j += 2)
                buf[i] = Convert.ToByte(str.Substring(j, 2), 16);
            return buf;
        }

        /// <summary>
        /// Encodes a  byte array into a hex string.
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        public static string EncodeHex(byte[] buffer)
        {
            var b = new StringBuilder(buffer.Length * 2);
            for (int i = 0; i < buffer.Length; i++)
                b.AppendFormat("{0:x2}", (uint)buffer[i]);
            return b.ToString();
        }

    }

}
