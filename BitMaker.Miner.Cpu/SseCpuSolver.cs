using System;

namespace BitMaker.Miner.Cpu
{

    public class SseCpuSolver : CpuSolver
    {

        public override unsafe uint? Solve(CpuMiner cpu, Work work, uint* round1State, byte* round1Block1, uint* round2State, byte* round2Block1)
        {
            // checked periodically to report progress and determine if the hasher should continue
            BitMaker.Utils.Native.CheckDelegate check = hashCount =>
            {
                cpu.ReportHashes(hashCount);

                // check whether the program is terminating, or whether our work is expired
                if (work.BlockNumber < cpu.CurrentBlockNumber || cpu.IsCancellationRequested)
                    return false;

                return true;
            };

            return BitMaker.Utils.Native.SseCpuHasher.Solve(round1State, round1Block1, round2State, round2Block1, check);
        }

    }

}
