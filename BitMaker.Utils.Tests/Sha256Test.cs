using System;
using System.Text;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using BitMaker.Miner;
using BitMaker.Miner.Cpu;

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

        ///// <summary>
        ///// Test class to run a solver against.
        ///// </summary>
        //private class CpuMinerStatus : ICpuSolverStatus
        //{

        //    private int hashCount = 0;

        //    /// <summary>
        //    /// Stops the solver when it's hash count exceeds the solution.
        //    /// </summary>
        //    /// <param name="hashes"></param>
        //    /// <returns></returns>
        //    public bool Check(uint hashes)
        //    {
        //        return (hashCount += (int)hashes) <= 3000;
        //    }
        //}

        [TestMethod]
        public unsafe void BlockHeaderHashTest()
        {
            // sample work with immediate solution
            var work = new Work()
            {
                BlockNumber = 0,
                Header = Memory.Decode("00000001d915b8fd2face61c6fe22ab76cad5f46c11cebab697dbd9e00000804000000008fe5f19cbdd55b40db93be7ef8ae249e0b21ec6e29c833b186404de0de205cc54e0022ac1a132185007d1adf000000800000000000000000000000000000000000000000000000000000000000000000000000000000000080020000"),
                Target = Memory.Decode("ffffffffffffffffffffffffffffffffffffffffffffffffffffffff00000000"),
            };

            // allocate buffers to hold hashing work
            byte[] round1Blocks = Sha256.AllocateInputBuffer(80);
            uint[] round1State = Sha256.AllocateStateBuffer();
            byte[] round2Blocks = Sha256.AllocateInputBuffer(Sha256.SHA256_HASH_SIZE);
            uint[] round2State = Sha256.AllocateStateBuffer();
            byte[] hash = Sha256.AllocateHashBuffer();

            fixed (byte* round1BlocksPtr = round1Blocks, round2BlocksPtr = round2Blocks, hashPtr = hash, targetPtr = work.Target)
            fixed (uint* round1StatePtr = round1State, round2StatePtr = round2State)
            {
                byte* round1Block1Ptr = round1BlocksPtr;
                byte* round1Block2Ptr = round1BlocksPtr + Sha256.SHA256_BLOCK_SIZE;
                byte* round2Block1Ptr = round2BlocksPtr;

                // header arrives in big endian, convert to host
                fixed (byte* workHeaderPtr = work.Header)
                    Memory.ReverseEndian((uint*)workHeaderPtr, (uint*)round1BlocksPtr, 20);

                // prepare states and blocks
                Sha256.Initialize(round1StatePtr);
                Sha256.Initialize(round2StatePtr);
                Sha256.Prepare(round1Block1Ptr, 80, 0);
                Sha256.Prepare(round1Block2Ptr, 80, 1);
                Sha256.Prepare(round2BlocksPtr, Sha256.SHA256_HASH_SIZE, 0);

                // hash first half of header
                Sha256.Transform(round1StatePtr, round1Block1Ptr);
                Sha256.Transform(round1StatePtr, round1Block2Ptr, (uint*)round2Block1Ptr);
                Sha256.Transform(round2StatePtr, round2Block1Ptr);
                Sha256.Finalize(round2StatePtr, hashPtr);

                for (int i = 7; i >= 0; i--)
                    Assert.IsFalse(((uint*)hashPtr)[i] > ((uint*)targetPtr)[i]);
            }

        }

    }

}