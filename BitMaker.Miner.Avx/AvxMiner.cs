using System.Threading;

using BitMaker.Miner.Cpu;
using BitMaker.Utils.Native;

namespace BitMaker.Miner.Avx
{

    public class AvxMiner : CpuMiner
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="cpu"></param>
        public AvxMiner(IMinerContext context, CpuResource cpu)
            : base(context, cpu)
        {

        }

        /// <summary>
        /// Implements the search function by passing it to native code.
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
            var check = (CheckDelegate)(i =>
            {
                // report hashes to context
                Context.ReportHashes(this, i);

                // abort if we are working on stale work, or if instructed to
                return work.Pool.CurrentBlockNumber == work.BlockNumber && !CancellationToken.IsCancellationRequested;
            });

            // dispatch work to native implementation
            return SseMinerUtils.Search(round1State, round1Block2, round2State, round2Block1, check);
        }

    }

}
