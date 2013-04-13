using BitMaker.Miner.Cpu;

namespace BitMaker.Miner.Avx
{

    /// <summary>
    /// Factory for <see cref="AvxMiner"/>.
    /// </summary>
    [MinerFactory]
    public class AvxMinerFactory : CpuMinerFactory
    {

        /// <summary>
        /// Starts a new instance of the miner.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="resource"></param>
        /// <returns></returns>
        public override IMiner StartMiner(IMinerContext context, CpuResource resource)
        {
            var miner = new AvxMiner(context, (CpuResource)resource);
            miner.Start();

            return miner;
        }

    }

}
