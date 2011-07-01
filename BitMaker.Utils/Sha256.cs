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
        /// Constants.
        /// </summary>
        public static uint[] K = new uint[]
        {
            0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
            0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
            0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
            0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
            0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
            0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
            0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
            0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2,
        };

        /// <summary>
        /// Initialization vectors.
        /// </summary>
        private static uint[] H0 = new uint[]
        {
            0x6a09e667, 0xbb67ae85, 0x3c6ef372, 0xa54ff53a,
            0x510e527f, 0x9b05688c, 0x1f83d9ab, 0x5be0cd19,
        };

        private static uint Shr(uint word, int shift)
        {
            return word >> shift;
        }

        public static uint Rotr(uint word, int shift)
        {
            return (word >> shift) | (word << (32 - shift));
        }

        public static uint Ch(uint x, uint y, uint z)
        {
            return z ^ (x & (y ^ z));
        }

        public static uint Maj(uint x, uint y, uint z)
        {
            return (x & y) | (z & (x | y));
        }

        public static uint Sigma0(uint x)
        {
            return Rotr(x, 2) ^ Rotr(x, 13) ^ Rotr(x, 22);
        }

        public static uint Sigma1(uint x)
        {
            return Rotr(x, 6) ^ Rotr(x, 11) ^ Rotr(x, 25);
        }

        public static uint sigma0(uint x)
        {
            return Rotr(x, 7) ^ Rotr(x, 18) ^ Shr(x, 3);
        }

        public static uint sigma1(uint x)
        {
            return Rotr(x, 17) ^ Rotr(x, 19) ^ Shr(x, 10);
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
            fixed (uint* _h = H0)
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

                // swap endianness of trailing length and insert as two integers
                uint sz1 = (uint)(((ulong)size * 8UL) << 32);
                uint sz2 = (uint)(((ulong)size * 8UL));
                ((uint*)block)[p / sizeof(uint) + 0] = Memory.ReverseEndian(sz1);
                ((uint*)block)[p / sizeof(uint) + 1] = Memory.ReverseEndian(sz2);
            }

            Memory.ReverseEndian((uint*)block, SHA256_BLOCK_SIZE / sizeof(uint));
        }

        /// <summary>
        /// Performs a SHA-256 round on the input registers, and input schedule <paramref name="W"/>, for round <paramref name="r"/>.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        /// <param name="d"></param>
        /// <param name="e"></param>
        /// <param name="f"></param>
        /// <param name="g"></param>
        /// <param name="h"></param>
        /// <param name="W"></param>
        /// <param name="r"></param>
        public static unsafe void Round(ref uint a, ref uint b, ref uint c, ref uint d, ref uint e, ref uint f, ref uint g, ref uint h, uint* W, int r)
        {
            uint t1 = h + Sigma1(e) + Ch(e, f, g) + K[r] + W[r];
            uint t2 = Sigma0(a) + Maj(a, b, c);

            h = g;
            g = f;
            f = e;
            e = d + t1;
            d = c;
            c = b;
            b = a;
            a = t1 + t2;
        }

        /// <summary>
        /// Creates the message schedule for the block.
        /// </summary>
        /// <param name="block"></param>
        /// <param name="W"></param>
        public static unsafe void Schedule(byte* block, uint* W)
        {
            // first 16 bytes of schedule
            for (int i = 0; i < 16; i++)
                W[i] = ((uint*)block)[i];

            // expand remainder of schedule
            for (int i = 16; i < 64; i++)
                W[i] = sigma1(W[i - 2]) + W[i - 7] + sigma0(W[i - 15]) + W[i - 16];
        }

        /// <summary>
        /// Updates the hash state <paramref name="state"/> with the input values, writing the new state into <param
        /// name="dst"/>.
        /// </summary>
        /// <param name="state"></param>
        /// <param name="block"></param>
        /// <param name="dst"></param>
        public static unsafe void Transform(uint* state, byte* block, uint* dst)
        {
            uint* H = stackalloc uint[8];
            uint* W = stackalloc uint[64];

            // create message schedule
            Schedule(block, W);

            // read existing state
            for (int i = 0; i < 8; i++)
                H[i] = state[i];

            // apply SHA-256 rounds to schedule
            for (int i = 0; i < 64; i++)
                Round(ref H[0], ref H[1], ref H[2], ref H[3], ref H[4], ref H[5], ref H[6], ref H[7], W, i);

            // update destination state
            for (int i = 0; i < 8; i++)
                dst[i] = state[i] + H[i];
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

        /// <summary>
        /// Finalize the digest.
        /// </summary>
        /// <param name="state"></param>
        /// <param name="output"></param>
        public static unsafe void Finalize(uint* state, byte* output)
        {
            if (BitConverter.IsLittleEndian)
                Memory.ReverseEndian(state, (uint*)output, SHA256_STATE_SIZE);
            else
                Memory.Copy((byte*)state, output, SHA256_HASH_SIZE);
        }

    }

}
