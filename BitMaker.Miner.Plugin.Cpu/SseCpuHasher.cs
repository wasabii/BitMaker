using System;

using BitMaker.Utils;
using BitMaker.Utils.Native;

namespace BitMaker.Miner.Plugin.Cpu
{

    public class SseCpuHasher : CpuHasher
    {

        public override unsafe uint? Solve(CpuMiner cpu, Work work, uint* round1State, byte* round1Block1, uint* round2State, byte* round2Block1)
        {
            // checked periodically to report progress and determine if the hasher should continue
            BitMaker.Utils.Native.CheckDelegate check = hashCount =>
            {
                Console.WriteLine("check");

                cpu.ReportHashes(hashCount);

                if (work.CancellationToken.IsCancellationRequested || cpu.IsCancellationRequested)
                    return false;

                // current block number has changed, our work is invalid
                if (work.BlockNumber < cpu.CurrentBlockNumber)
                    return false;

                return true;
            };

            return BitMaker.Utils.Native.SseCpuHasher.Solve(round1State, round1Block1, round2State, round2Block1, check);
        }

    }

}
