using BitMaker.Miner.Cpu;

namespace BitMaker.Miner.Sse
{

    /// <summary>
    /// Factory for <see cref="SseMiner"/>.
    /// </summary>
    [MinerFactory]
    public class SseMinerFactory : CpuMinerFactory
    {

        /// <summary>
        /// Starts a new instance of the miner.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="resource"></param>
        /// <returns></returns>
        public override IMiner StartMiner(IMinerContext context, CpuResource resource)
        {
            var miner = new SseMiner(context, (CpuResource)resource);
            miner.Start();

            return miner;
        }

    }

}
