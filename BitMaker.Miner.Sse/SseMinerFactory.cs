using System.Collections.Generic;
using System.Linq;

using BitMaker.Miner.Cpu;

namespace BitMaker.Miner.Sse
{

    /// <summary>
    /// Factory for <see cref="SseMiner"/>.
    /// </summary>
    [MinerFactory]
    public class SseMinerFactory : CpuMinerFactory
    {

        public override IEnumerable<MinerResource> Resources
        {
            get { return base.Resources; }
            //get { return Enumerable.Empty<CpuResource>(); }
        }

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
