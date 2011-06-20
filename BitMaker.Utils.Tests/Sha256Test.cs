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

        [TestMethod]
        public unsafe void StringHashTest()
        {
            var data = Encoding.UTF8.GetBytes("abcabcabcabcabcabcabcabcabcabcabcabcabcabcabcabcabcabcabcabcabcabcabcabcabcabcabcabc");
            var work = Sha256.AllocateInputBuffer(data.Length);
            var state = Sha256.AllocateStateBuffer();
            var hash = Sha256.AllocateHashBuffer();

            // reference data for comparison against
            var refHash = System.Security.Cryptography.SHA256.Create().ComputeHash(data);

            fixed (byte* dataPtr = data, workPtr = work, hashPtr = hash)
            fixed (uint* statePtr = state)
            {
                // create working copy of data
                Memory.Copy(dataPtr, workPtr, data.Length);

                // initialize state with starting values
                Sha256.Initialize(statePtr);
                Assert.AreEqual(((uint*)statePtr)[0], 0x6a09e667U);
                Assert.AreEqual(((uint*)statePtr)[7], 0x5be0cd19U);

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

}
