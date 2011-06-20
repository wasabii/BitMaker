using System;

namespace BitMaker.Utils
{

    public static class Sha256
    {

        /// <summary>
        /// Minimum amount of bytes required for SHA-256 padding.
        /// </summary>
        public const int SHA256_MIN_PAD_SIZE = sizeof(byte) + sizeof(ulong);

        /// <summary>
        /// Length of a SHA-256 input block in bytes.
        /// </summary>
        public const int SHA256_BLOCK_SIZE = 64;

        /// <summary>
        /// Length of the SHA-256 state in uints.
        /// </summary>
        public const int SHA256_STATE_SIZE = 8;

        /// <summary>
        /// Length of a SHA-256 hash in bytes.
        /// </summary>
        public const int SHA256_HASH_SIZE = 32;

        /// <summary>
        /// Initialization vectors.
        /// </summary>
        private static uint[] H = new uint[]
        {
            0x6a09e667, 0xbb67ae85, 0x3c6ef372, 0xa54ff53a,
            0x510e527f, 0x9b05688c, 0x1f83d9ab, 0x5be0cd19,
        };

        private static uint SHR(uint word, int shift)
        {
            return word >> shift;
        }

        private static uint ROTR(uint word, int shift)
        {
            return (word >> shift) | (word << (32 - shift));
        }

        private static uint Ch(uint x, uint y, uint z)
        {
            return z ^ (x & (y ^ z));
        }

        private static uint Maj(uint x, uint y, uint z)
        {
            return (x & y) | (z & (x | y));
        }

        private static uint Sigma0(uint x)
        {
            return ROTR(x, 2) ^ ROTR(x, 13) ^ ROTR(x, 22);
        }

        private static uint Sigma1(uint x)
        {
            return ROTR(x, 6) ^ ROTR(x, 11) ^ ROTR(x, 25);
        }

        private static uint sigma0(uint x)
        {
            return ROTR(x, 7) ^ ROTR(x, 18) ^ SHR(x, 3);
        }

        private static uint sigma1(uint x)
        {
            return ROTR(x, 17) ^ ROTR(x, 19) ^ SHR(x, 10);
        }

        /// <summary>
        /// Allocates minimum amount of space to hold data of the given size (in bytes), including padding.
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        public static byte[] AllocateInputBuffer(int size)
        {
            return new byte[((size + SHA256_MIN_PAD_SIZE) / SHA256_BLOCK_SIZE + 1) * SHA256_BLOCK_SIZE];
        }

        /// <summary>
        /// Allocates space into which an input buffer is transformed.
        /// </summary>
        /// <returns></returns>
        public static uint[] AllocateStateBuffer()
        {
            return new uint[SHA256_STATE_SIZE];
        }

        /// <summary>
        /// Allocates space into which a state buffer is finalized into a hash.
        /// </summary>
        /// <returns></returns>
        public static byte[] AllocateHashBuffer()
        {
            return new byte[SHA256_HASH_SIZE];
        }

        /// <summary>
        /// Initializes <paramref name="state"/> with the SHA-256 initial state. Ensure <paramref name="state"/> is
        /// SHA256_STATE_SIZE bytes in length.
        /// </summary>
        /// <param name="output"></param>
        public static unsafe void Initialize(uint* state)
        {
            // initialize state with init value
            fixed (uint* _h = H)
                Memory.Copy(_h, state, SHA256_STATE_SIZE);
        }

        /// <summary>
        /// Applies the required SHA-256 padding to the data given and ensures proper byte order.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="size"></param>
        /// <param name="currBlock"></param>
        public static unsafe void Prepare(byte[] data, int size, int currBlock)
        {
            if (data.Length != (size / SHA256_BLOCK_SIZE + 1) * SHA256_BLOCK_SIZE)
                throw new InsufficientMemoryException("Passed buffer is not of the right size.");

            fixed (byte* dataPtr = data)
                Prepare(dataPtr + currBlock * SHA256_BLOCK_SIZE, size, currBlock);
        }

        /// <summary>
        /// Applies the required SHA-256 padding to the data given and ensures proper byte order. <paramref
        /// name="block"/> should point to the beginning of the block number specified by <paramref name="currBlock"/>.
        /// </summary>
        /// <param name="size">Total length of the data being hashed</param>
        public static unsafe void Prepare(byte* block, int size, int currBlock)
        {
            var lastBlock = (size + SHA256_MIN_PAD_SIZE) / SHA256_BLOCK_SIZE;

            // if we are working on the last block
            if (currBlock == lastBlock)
            {
                // append '1' immediately after data
                block[size % SHA256_BLOCK_SIZE] = 0x80;

                // zero between '1' and length field
                Memory.Zero(block + size % SHA256_BLOCK_SIZE + 1, SHA256_BLOCK_SIZE - (size % SHA256_BLOCK_SIZE) - 1 - sizeof(ulong));

                // starting position of trailing length, in long
                var p = SHA256_BLOCK_SIZE - sizeof(ulong);

                if (BitConverter.IsLittleEndian)
                {
                    // swap endianness of trailing length and insert as two integers
                    uint sz1 = (uint)(((ulong)size * 8UL) << 32);
                    uint sz2 = (uint)(((ulong)size * 8UL));
                    ((uint*)block)[p / sizeof(uint) + 0] = Memory.ReverseEndian(sz1);
                    ((uint*)block)[p / sizeof(uint) + 1] = Memory.ReverseEndian(sz2);
                }
                else
                    ((ulong*)block)[p] = (ulong)size * 8UL;
            }

            if (BitConverter.IsLittleEndian)
                Memory.ReverseEndian((uint*)block, SHA256_BLOCK_SIZE / sizeof(uint));
        }

        /// <summary>
        /// Updates the hash state <paramref name="state"/> with the input values, writing the new state into <param
        /// name="dst"/>.
        /// </summary>
        public static unsafe void Transform(uint* state, byte* block, uint* dst)
        {
            uint a, b, c, d, e, f, g, h, t1, t2;
            uint* W = stackalloc uint[64];

            for (int i = 0; i < 16; i++)
                W[i] = ((uint*)block)[i];

            for (int i = 16; i < 64; i++)
                W[i] = sigma1(W[i - 2]) + W[i - 7] + sigma0(W[i - 15]) + W[i - 16];

            // read existing state
            a = state[0];
            b = state[1];
            c = state[2];
            d = state[3];
            e = state[4];
            f = state[5];
            g = state[6];
            h = state[7];

            t1 = h + Sigma1(e) + Ch(e, f, g) + 0x428a2f98 + W[0];
            t2 = Sigma0(a) + Maj(a, b, c); d += t1; h = t1 + t2;
            t1 = g + Sigma1(d) + Ch(d, e, f) + 0x71374491 + W[1];
            t2 = Sigma0(h) + Maj(h, a, b); c += t1; g = t1 + t2;
            t1 = f + Sigma1(c) + Ch(c, d, e) + 0xb5c0fbcf + W[2];
            t2 = Sigma0(g) + Maj(g, h, a); b += t1; f = t1 + t2;
            t1 = e + Sigma1(b) + Ch(b, c, d) + 0xe9b5dba5 + W[3];
            t2 = Sigma0(f) + Maj(f, g, h); a += t1; e = t1 + t2;
            t1 = d + Sigma1(a) + Ch(a, b, c) + 0x3956c25b + W[4];
            t2 = Sigma0(e) + Maj(e, f, g); h += t1; d = t1 + t2;
            t1 = c + Sigma1(h) + Ch(h, a, b) + 0x59f111f1 + W[5];
            t2 = Sigma0(d) + Maj(d, e, f); g += t1; c = t1 + t2;
            t1 = b + Sigma1(g) + Ch(g, h, a) + 0x923f82a4 + W[6];
            t2 = Sigma0(c) + Maj(c, d, e); f += t1; b = t1 + t2;
            t1 = a + Sigma1(f) + Ch(f, g, h) + 0xab1c5ed5 + W[7];
            t2 = Sigma0(b) + Maj(b, c, d); e += t1; a = t1 + t2;

            t1 = h + Sigma1(e) + Ch(e, f, g) + 0xd807aa98 + W[8];
            t2 = Sigma0(a) + Maj(a, b, c); d += t1; h = t1 + t2;
            t1 = g + Sigma1(d) + Ch(d, e, f) + 0x12835b01 + W[9];
            t2 = Sigma0(h) + Maj(h, a, b); c += t1; g = t1 + t2;
            t1 = f + Sigma1(c) + Ch(c, d, e) + 0x243185be + W[10];
            t2 = Sigma0(g) + Maj(g, h, a); b += t1; f = t1 + t2;
            t1 = e + Sigma1(b) + Ch(b, c, d) + 0x550c7dc3 + W[11];
            t2 = Sigma0(f) + Maj(f, g, h); a += t1; e = t1 + t2;
            t1 = d + Sigma1(a) + Ch(a, b, c) + 0x72be5d74 + W[12];
            t2 = Sigma0(e) + Maj(e, f, g); h += t1; d = t1 + t2;
            t1 = c + Sigma1(h) + Ch(h, a, b) + 0x80deb1fe + W[13];
            t2 = Sigma0(d) + Maj(d, e, f); g += t1; c = t1 + t2;
            t1 = b + Sigma1(g) + Ch(g, h, a) + 0x9bdc06a7 + W[14];
            t2 = Sigma0(c) + Maj(c, d, e); f += t1; b = t1 + t2;
            t1 = a + Sigma1(f) + Ch(f, g, h) + 0xc19bf174 + W[15];
            t2 = Sigma0(b) + Maj(b, c, d); e += t1; a = t1 + t2;

            t1 = h + Sigma1(e) + Ch(e, f, g) + 0xe49b69c1 + W[16];
            t2 = Sigma0(a) + Maj(a, b, c); d += t1; h = t1 + t2;
            t1 = g + Sigma1(d) + Ch(d, e, f) + 0xefbe4786 + W[17];
            t2 = Sigma0(h) + Maj(h, a, b); c += t1; g = t1 + t2;
            t1 = f + Sigma1(c) + Ch(c, d, e) + 0x0fc19dc6 + W[18];
            t2 = Sigma0(g) + Maj(g, h, a); b += t1; f = t1 + t2;
            t1 = e + Sigma1(b) + Ch(b, c, d) + 0x240ca1cc + W[19];
            t2 = Sigma0(f) + Maj(f, g, h); a += t1; e = t1 + t2;
            t1 = d + Sigma1(a) + Ch(a, b, c) + 0x2de92c6f + W[20];
            t2 = Sigma0(e) + Maj(e, f, g); h += t1; d = t1 + t2;
            t1 = c + Sigma1(h) + Ch(h, a, b) + 0x4a7484aa + W[21];
            t2 = Sigma0(d) + Maj(d, e, f); g += t1; c = t1 + t2;
            t1 = b + Sigma1(g) + Ch(g, h, a) + 0x5cb0a9dc + W[22];
            t2 = Sigma0(c) + Maj(c, d, e); f += t1; b = t1 + t2;
            t1 = a + Sigma1(f) + Ch(f, g, h) + 0x76f988da + W[23];
            t2 = Sigma0(b) + Maj(b, c, d); e += t1; a = t1 + t2;

            t1 = h + Sigma1(e) + Ch(e, f, g) + 0x983e5152 + W[24];
            t2 = Sigma0(a) + Maj(a, b, c); d += t1; h = t1 + t2;
            t1 = g + Sigma1(d) + Ch(d, e, f) + 0xa831c66d + W[25];
            t2 = Sigma0(h) + Maj(h, a, b); c += t1; g = t1 + t2;
            t1 = f + Sigma1(c) + Ch(c, d, e) + 0xb00327c8 + W[26];
            t2 = Sigma0(g) + Maj(g, h, a); b += t1; f = t1 + t2;
            t1 = e + Sigma1(b) + Ch(b, c, d) + 0xbf597fc7 + W[27];
            t2 = Sigma0(f) + Maj(f, g, h); a += t1; e = t1 + t2;
            t1 = d + Sigma1(a) + Ch(a, b, c) + 0xc6e00bf3 + W[28];
            t2 = Sigma0(e) + Maj(e, f, g); h += t1; d = t1 + t2;
            t1 = c + Sigma1(h) + Ch(h, a, b) + 0xd5a79147 + W[29];
            t2 = Sigma0(d) + Maj(d, e, f); g += t1; c = t1 + t2;
            t1 = b + Sigma1(g) + Ch(g, h, a) + 0x06ca6351 + W[30];
            t2 = Sigma0(c) + Maj(c, d, e); f += t1; b = t1 + t2;
            t1 = a + Sigma1(f) + Ch(f, g, h) + 0x14292967 + W[31];
            t2 = Sigma0(b) + Maj(b, c, d); e += t1; a = t1 + t2;

            t1 = h + Sigma1(e) + Ch(e, f, g) + 0x27b70a85 + W[32];
            t2 = Sigma0(a) + Maj(a, b, c); d += t1; h = t1 + t2;
            t1 = g + Sigma1(d) + Ch(d, e, f) + 0x2e1b2138 + W[33];
            t2 = Sigma0(h) + Maj(h, a, b); c += t1; g = t1 + t2;
            t1 = f + Sigma1(c) + Ch(c, d, e) + 0x4d2c6dfc + W[34];
            t2 = Sigma0(g) + Maj(g, h, a); b += t1; f = t1 + t2;
            t1 = e + Sigma1(b) + Ch(b, c, d) + 0x53380d13 + W[35];
            t2 = Sigma0(f) + Maj(f, g, h); a += t1; e = t1 + t2;
            t1 = d + Sigma1(a) + Ch(a, b, c) + 0x650a7354 + W[36];
            t2 = Sigma0(e) + Maj(e, f, g); h += t1; d = t1 + t2;
            t1 = c + Sigma1(h) + Ch(h, a, b) + 0x766a0abb + W[37];
            t2 = Sigma0(d) + Maj(d, e, f); g += t1; c = t1 + t2;
            t1 = b + Sigma1(g) + Ch(g, h, a) + 0x81c2c92e + W[38];
            t2 = Sigma0(c) + Maj(c, d, e); f += t1; b = t1 + t2;
            t1 = a + Sigma1(f) + Ch(f, g, h) + 0x92722c85 + W[39];
            t2 = Sigma0(b) + Maj(b, c, d); e += t1; a = t1 + t2;

            t1 = h + Sigma1(e) + Ch(e, f, g) + 0xa2bfe8a1 + W[40];
            t2 = Sigma0(a) + Maj(a, b, c); d += t1; h = t1 + t2;
            t1 = g + Sigma1(d) + Ch(d, e, f) + 0xa81a664b + W[41];
            t2 = Sigma0(h) + Maj(h, a, b); c += t1; g = t1 + t2;
            t1 = f + Sigma1(c) + Ch(c, d, e) + 0xc24b8b70 + W[42];
            t2 = Sigma0(g) + Maj(g, h, a); b += t1; f = t1 + t2;
            t1 = e + Sigma1(b) + Ch(b, c, d) + 0xc76c51a3 + W[43];
            t2 = Sigma0(f) + Maj(f, g, h); a += t1; e = t1 + t2;
            t1 = d + Sigma1(a) + Ch(a, b, c) + 0xd192e819 + W[44];
            t2 = Sigma0(e) + Maj(e, f, g); h += t1; d = t1 + t2;
            t1 = c + Sigma1(h) + Ch(h, a, b) + 0xd6990624 + W[45];
            t2 = Sigma0(d) + Maj(d, e, f); g += t1; c = t1 + t2;
            t1 = b + Sigma1(g) + Ch(g, h, a) + 0xf40e3585 + W[46];
            t2 = Sigma0(c) + Maj(c, d, e); f += t1; b = t1 + t2;
            t1 = a + Sigma1(f) + Ch(f, g, h) + 0x106aa070 + W[47];
            t2 = Sigma0(b) + Maj(b, c, d); e += t1; a = t1 + t2;

            t1 = h + Sigma1(e) + Ch(e, f, g) + 0x19a4c116 + W[48];
            t2 = Sigma0(a) + Maj(a, b, c); d += t1; h = t1 + t2;
            t1 = g + Sigma1(d) + Ch(d, e, f) + 0x1e376c08 + W[49];
            t2 = Sigma0(h) + Maj(h, a, b); c += t1; g = t1 + t2;
            t1 = f + Sigma1(c) + Ch(c, d, e) + 0x2748774c + W[50];
            t2 = Sigma0(g) + Maj(g, h, a); b += t1; f = t1 + t2;
            t1 = e + Sigma1(b) + Ch(b, c, d) + 0x34b0bcb5 + W[51];
            t2 = Sigma0(f) + Maj(f, g, h); a += t1; e = t1 + t2;
            t1 = d + Sigma1(a) + Ch(a, b, c) + 0x391c0cb3 + W[52];
            t2 = Sigma0(e) + Maj(e, f, g); h += t1; d = t1 + t2;
            t1 = c + Sigma1(h) + Ch(h, a, b) + 0x4ed8aa4a + W[53];
            t2 = Sigma0(d) + Maj(d, e, f); g += t1; c = t1 + t2;
            t1 = b + Sigma1(g) + Ch(g, h, a) + 0x5b9cca4f + W[54];
            t2 = Sigma0(c) + Maj(c, d, e); f += t1; b = t1 + t2;
            t1 = a + Sigma1(f) + Ch(f, g, h) + 0x682e6ff3 + W[55];
            t2 = Sigma0(b) + Maj(b, c, d); e += t1; a = t1 + t2;

            t1 = h + Sigma1(e) + Ch(e, f, g) + 0x748f82ee + W[56];
            t2 = Sigma0(a) + Maj(a, b, c); d += t1; h = t1 + t2;
            t1 = g + Sigma1(d) + Ch(d, e, f) + 0x78a5636f + W[57];
            t2 = Sigma0(h) + Maj(h, a, b); c += t1; g = t1 + t2;
            t1 = f + Sigma1(c) + Ch(c, d, e) + 0x84c87814 + W[58];
            t2 = Sigma0(g) + Maj(g, h, a); b += t1; f = t1 + t2;
            t1 = e + Sigma1(b) + Ch(b, c, d) + 0x8cc70208 + W[59];
            t2 = Sigma0(f) + Maj(f, g, h); a += t1; e = t1 + t2;
            t1 = d + Sigma1(a) + Ch(a, b, c) + 0x90befffa + W[60];
            t2 = Sigma0(e) + Maj(e, f, g); h += t1; d = t1 + t2;
            t1 = c + Sigma1(h) + Ch(h, a, b) + 0xa4506ceb + W[61];
            t2 = Sigma0(d) + Maj(d, e, f); g += t1; c = t1 + t2;
            t1 = b + Sigma1(g) + Ch(g, h, a) + 0xbef9a3f7 + W[62];
            t2 = Sigma0(c) + Maj(c, d, e); f += t1; b = t1 + t2;
            t1 = a + Sigma1(f) + Ch(f, g, h) + 0xc67178f2 + W[63];
            t2 = Sigma0(b) + Maj(b, c, d); e += t1; a = t1 + t2;

            // write new state
            dst[0] = state[0] + a;
            dst[1] = state[1] + b;
            dst[2] = state[2] + c;
            dst[3] = state[3] + d;
            dst[4] = state[4] + e;
            dst[5] = state[5] + f;
            dst[6] = state[6] + g;
            dst[7] = state[7] + h;
        }

        /// <summary>
        /// Updates the hash state <paramref name="state"/> with the input values, writing the new state back into
        /// <paramref name="state" />
        /// </summary>
        /// <param name="state"></param>
        /// <param name="block"></param>
        public static unsafe void Transform(uint* state, byte* block)
        {
            Transform(state, block, state);
        }

        public static unsafe void Finalize(uint* state, byte* output)
        {
            if (BitConverter.IsLittleEndian)
                Memory.ReverseEndian(state, (uint*)output, SHA256_HASH_SIZE / sizeof(uint));
            else
                Memory.Copy((byte*)state, output, SHA256_HASH_SIZE);
        }

    }

}
