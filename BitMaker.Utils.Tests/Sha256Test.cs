using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Runtime.InteropServices;

namespace BitMaker.Utils.Tests
{

    [TestClass]
    public class Sha256Test
    {

        private static readonly System.Security.Cryptography.SHA256 sha256 = System.Security.Cryptography.SHA256.Create();

        private static readonly string[] tests = new string[]
        {
            "abc",
            "abcabcabcabcabcabcabcabcabcabc",
            "abcabcabcabcabcabcabcabcabcabcabcabcabcabcabcabcabcabcabcabcabcabcabcabcabcabcabcabcabcabcabcabc",
        };

        [TestMethod]
        public unsafe void InitializeTest()
        {
            var state = Sha256.AllocateStateBuffer();

            fixed (uint* statePtr = state)
            {
                Sha256.Initialize(statePtr);
                Assert.AreEqual(((uint*)statePtr)[0], 0x6a09e667U);
                Assert.AreEqual(((uint*)statePtr)[7], 0x5be0cd19U);
            }
        }

        [TestMethod]
        public unsafe void BasicHashTest()
        {
            foreach (var test in tests)
            {
                var data = Encoding.UTF8.GetBytes(test);
                var work = Sha256.AllocateInputBuffer(data.Length);
                var state = Sha256.AllocateStateBuffer();
                var hash = Sha256.AllocateHashBuffer();

                // reference data for comparison against
                var refHash = sha256.ComputeHash(data);

                fixed (byte* dataPtr = data, workPtr = work, hashPtr = hash)
                fixed (uint* statePtr = state)
                {
                    // create working copy of data
                    Memory.Copy(dataPtr, workPtr, data.Length);

                    Sha256.Initialize(statePtr);

                    for (int i = 0; i < work.Length / Sha256.SHA256_BLOCK_SIZE; i++)
                    {
                        Sha256.Prepare(workPtr + i * Sha256.SHA256_BLOCK_SIZE, data.Length, i);
                        Sha256.Transform(statePtr, workPtr + i * Sha256.SHA256_BLOCK_SIZE);
                    }

                    // output hash
                    Sha256.Finalize(statePtr, hashPtr);
                }

                for (int i = 0; i < hash.Length; i++)
                    Assert.AreEqual(refHash[i], hash[i]);
            }
        }

        [TestMethod]
        public unsafe void DoubleHashTest()
        {
            foreach (var test in tests)
            {
                var data = Encoding.UTF8.GetBytes(test);
                var work = Sha256.AllocateInputBuffer(Math.Max(data.Length, Sha256.SHA256_HASH_SIZE)); // enough space for original hash and hash of hash
                var state = Sha256.AllocateStateBuffer();
                var hash = Sha256.AllocateHashBuffer();

                // reference data for comparison against
                var refHash = sha256.ComputeHash(sha256.ComputeHash(data));

                fixed (byte* dataPtr = data, workPtr = work, hashPtr = hash)
                fixed (uint* statePtr = state)
                {
                    // create working copy of data
                    Memory.Copy(dataPtr, workPtr, data.Length);

                    Sha256.Initialize(statePtr);

                    for (int i = 0; i < work.Length / Sha256.SHA256_BLOCK_SIZE; i++)
                    {
                        Sha256.Prepare(workPtr + i * Sha256.SHA256_BLOCK_SIZE, data.Length, i);
                        Sha256.Transform(statePtr, workPtr + i * Sha256.SHA256_BLOCK_SIZE);
                    }

                    // output first hash
                    Sha256.Finalize(statePtr, hashPtr);

                    // copy hash back to work, and hash aagin
                    Memory.Copy(hashPtr, workPtr, Sha256.SHA256_HASH_SIZE);
                    Sha256.Initialize(statePtr);
                    Sha256.Prepare(workPtr, Sha256.SHA256_HASH_SIZE, 0);
                    Sha256.Transform(statePtr, workPtr);
                    Sha256.Finalize(statePtr, hashPtr);
                }

                for (int i = 0; i < hash.Length; i++)
                    Assert.AreEqual(refHash[i], hash[i]);
            }
        }

        [TestMethod]
        public unsafe void DoubleHashNoFinalizeTest()
        {
            foreach (var test in tests)
            {
                var data = Encoding.UTF8.GetBytes(test);
                var work = Sha256.AllocateInputBuffer(data.Length);
                var state = Sha256.AllocateStateBuffer();
                var hash = new byte[Sha256.SHA256_BLOCK_SIZE];

                // reference data for comparison against
                var refHash = sha256.ComputeHash(sha256.ComputeHash(data));

                fixed (byte* dataPtr = data, workPtr = work, hashPtr = hash)
                fixed (uint* statePtr = state)
                {
                    // create working copy of data
                    Memory.Copy(dataPtr, workPtr, data.Length);

                    Sha256.Initialize(statePtr);

                    for (int i = 0; i < work.Length / Sha256.SHA256_BLOCK_SIZE; i++)
                    {
                        Sha256.Prepare(workPtr + i * Sha256.SHA256_BLOCK_SIZE, data.Length, i);
                        Sha256.Transform(statePtr, workPtr + i * Sha256.SHA256_BLOCK_SIZE);
                    }

                    // prepare work block with padding
                    Sha256.Prepare(hashPtr, Sha256.SHA256_HASH_SIZE, 0);

                    // copy state directly into work block, already reversed
                    Memory.Copy(statePtr, (uint*)hashPtr, Sha256.SHA256_STATE_SIZE);

                    // build second hash
                    Sha256.Initialize(statePtr);
                    Sha256.Transform(statePtr, hashPtr);
                    Sha256.Finalize(statePtr, hashPtr);
                }

                for (int i = 0; i < Sha256.SHA256_HASH_SIZE; i++)
                    Assert.AreEqual(refHash[i], hash[i]);
            }
        }

        //[TestMethod]
        //public unsafe void BlockHeaderHashTest()
        //{
        //    var a = Memory.ReverseEndian(1308625456);
        //    var b = Memory.Encode(new uint[] { 2377719358 });

        //    byte[] workHeader = Memory.Decode("0000000195722e37c42b188b79487a29d08cfb7fa7962a8935ead9e60000038300000000b0a5360d1d8a902cf9c047cf395e181aa498ce84f15be5d6c4e5b1f0c94a33474e000a301a13218500000000000000800000000000000000000000000000000000000000000000000000000000000000000000000000000080020000");
        //    byte[] workTarget = Memory.Decode("ffffffffffffffffffffffffffffffffffffffffffffffffffffffff00000000");
        //    byte[] workNewHeader;

        //    /* This procedure is optimized based on the internals of the SHA-256 algorithm. As each block is transformed,
        //     * the state variable is updated. Finalizing the hash consists of reversing the byte order of the state.
        //     * Data to be hashed needs to have it's byte order reversed. Instead of reversing the first state to obtain
        //     * the first hash and then reversing it again, we output the transform of the header directly into a block
        //     * pre-padded to the size of a hash, and then transform that again using new state. This prevents the double
        //     * byte order swap.
        //     **/


        //    // allocate buffers to hold hashing work
        //    byte[] data = Sha256.AllocateInputBuffer(80);
        //    uint[] midstate = Sha256.AllocateStateBuffer();
        //    uint[] state = Sha256.AllocateStateBuffer();
        //    uint[] state2 = Sha256.AllocateStateBuffer();
        //    byte[] hash = new byte[Sha256.SHA256_BLOCK_SIZE];

        //    fixed (byte* workHeaderPtr = workHeader, workTargetPtr = workTarget)
        //    fixed (byte* dataPtr = data, hashPtr = hash)
        //    fixed (uint* midstatePtr = midstate, statePtr = state, state2Ptr = state2)
        //    {
        //        if (BitConverter.IsLittleEndian)
        //            // header arrives in big endian, convert to host
        //            Memory.ReverseEndian((uint*)workHeaderPtr, (uint*)dataPtr, 20);
        //        else
        //            // simply copy if conversion not required
        //            Memory.Copy((uint*)workHeaderPtr, (uint*)dataPtr, 20);

        //        // append '1' bit and trailing length
        //        Sha256.Prepare(dataPtr, 80, 0);
        //        Sha256.Prepare(dataPtr + Sha256.SHA256_BLOCK_SIZE, 80, 1);

        //        // hash first half of header
        //        Sha256.Initialize(midstatePtr);
        //        Sha256.Transform(midstatePtr, dataPtr);

        //        // prepare the block of the hash buffer for the second round, this data shouldn't be overwritten
        //        Sha256.Prepare(hashPtr, Sha256.SHA256_HASH_SIZE, 0);

        //        // read initial nonce value
        //        uint nonce = Memory.ReverseEndian(((uint*)dataPtr)[19]);

        //        // initial state
        //        Sha256.Initialize(statePtr);

        //        // test every possible nonce value
        //        while (nonce <= uint.MaxValue)
        //        {
        //            ((uint*)dataPtr)[19] = Memory.ReverseEndian(2377719358);

        //            // transform variable second half of block using saved state
        //            Sha256.Transform(midstatePtr, dataPtr + Sha256.SHA256_BLOCK_SIZE, (uint*)hashPtr);

        //            // compute second hash back into hash
        //            Sha256.Transform(statePtr, hashPtr, state2Ptr);
                    
        //            // the hash is byte order flipped state, quick check that state passes a test before doing work
        //            if (state2Ptr[7] == 0U)
        //            {
        //                // replace header data on work
        //                workNewHeader = new byte[80];
        //                fixed (byte* dstHeaderPtr = workNewHeader)
        //                    Memory.Copy((uint*)dataPtr, (uint*)dstHeaderPtr, 20);

        //                // finalize hash
        //                byte[] finalHash = Sha256.AllocateHashBuffer();
        //                fixed (byte* finalHashPtr = finalHash)
        //                    Sha256.Finalize(state2Ptr, finalHashPtr);

        //                // bitcoin hashes are byte order flipped SHA-256 hashes
        //                Array.Reverse(finalHash);

        //                // encode for display purposes
        //                var blockHash = Memory.Encode(finalHash);

        //                // display message indicating submission
        //                Console.WriteLine();
        //                Console.WriteLine();
        //                Console.WriteLine("Solution: {0}", blockHash);

        //                Console.WriteLine();
        //                Console.WriteLine();

        //                // header needs to have SHA-256 padding appended
        //                var data2 = Sha256.AllocateInputBuffer(80);

        //                // prepare header buffer with SHA-256
        //                Sha256.Prepare(data2, 80, 0);
        //                Sha256.Prepare(data2, 80, 1);

        //                // dump header data on top of padding
        //                Array.Copy(workNewHeader, data2, 80);

        //                // encode in proper format
        //                var param = Memory.Encode(data2);
        //            }

        //            // update the nonce value
        //            ((uint*)dataPtr)[19] = Memory.ReverseEndian(++nonce);
        //        }
        //    }
        //}

    }

}
