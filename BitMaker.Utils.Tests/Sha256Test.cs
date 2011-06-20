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

    }

}
