using System;

using BitMaker.Utils;

namespace BitMaker.Miner.Plugin.Cpu
{

    public class ManagedCpuHasher : CpuHasher
    {

        public override unsafe uint? Solve(CpuMiner cpu, Work work, uint* round1State, byte* round1Block1, uint* round2State, byte* round2Block1)
        {
            uint nonce = 0;

            byte* round1Block2 = round1Block1 + Sha256.SHA256_BLOCK_SIZE;
            uint* round2State2 = stackalloc uint[Sha256.SHA256_STATE_SIZE];

            for (; ; )
            {
                // transform variable second half of block using saved state from first block, into pre-padded round 2 block (end of first hash)
                Sha256.Transform(round1State, round1Block2, (uint*)round2Block1);

                // transform round 2 block into round 2 state (second hash)
                Sha256.Transform(round2State, round2Block1, round2State2);

                // test for potentially valid hash
                if (round2State2[7] == 0U)
                    return nonce;

                // at the end of our nonce values, we can't continue
                if (nonce == uint.MaxValue)
                    break;

                // update the nonce value
                ((uint*)round1Block2)[3] = Memory.ReverseEndian(++nonce);

                // only report and check for exit conditions every so often
                if (nonce % 1024 == 0)
                {
                    cpu.ReportHashes(1024);
                    if (work.CancellationToken.IsCancellationRequested || cpu.IsCancellationRequested)
                        return null;

                    // current block number has changed, our work is invalid
                    if (work.BlockNumber < cpu.CurrentBlockNumber)
                        break;
                }
            }

            return null;
        }

    }

}
