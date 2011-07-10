using System;
using System.Threading;

using BitMaker.Miner.Cpu;
using BitMaker.Utils;

namespace BitMaker.Miner.Managed
{

    /// <summary>
    /// Completely managed C# implementation of a miner.
    /// </summary>
    public class ManagedMiner : CpuMiner
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="cpu"></param>
        public ManagedMiner(IMinerContext context, CpuResource cpu)
            : base(context, cpu)
        {

        }

        /// <summary>
        /// Implements the search function in managed code.
        /// </summary>
        /// <param name="work"></param>
        /// <param name="round1State"></param>
        /// <param name="round1Block2"></param>
        /// <param name="round2State"></param>
        /// <param name="round2Block1"></param>
        /// <returns></returns>
        public override unsafe uint? Search(Work work, uint* round1State, byte* round1Block2, uint* round2State, byte* round2Block1)
        {
            // invoked periodically to report hashes and check status
            var check = (Func<uint, bool>)(i =>
            {
                // report hashes to context
                Context.ReportHashes(this, i);

                // abort if we are working on stale work, or if instructed to
                return work.Pool.CurrentBlockNumber == work.BlockNumber && !CancellationToken.IsCancellationRequested;
            });

            // starting nonce
            uint nonce = 0;

            // output for final hash
            uint* round2State2 = stackalloc uint[Sha256.SHA256_STATE_SIZE];

            while (true)
            {
                // update the nonce value
                ((uint*)round1Block2)[3] = nonce;

                // transform variable second half of block using saved state from first block, into pre-padded round 2 block (end of first hash)
                Sha256.Transform(round1State, round1Block2, (uint*)round2Block1);

                // transform round 2 block into round 2 state (second hash)
                Sha256.Transform(round2State, round2Block1, round2State2);

                // test for potentially valid hash
                if (round2State2[7] == 0U)
                    // actual nonce is flipped
                    return Memory.ReverseEndian(nonce);

                // only report and check for exit conditions every so often
                if ((++nonce % 65536) == 0)
                    if (!check(65536) || nonce == 0)
                        break;
            }

            return null;
        }

    }

}
