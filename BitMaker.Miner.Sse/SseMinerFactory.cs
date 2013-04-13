using System.Collections.Generic;
using System.Linq;

using BitMaker.Miner.Cpu;
using BitMaker.Utils.Native;

namespace BitMaker.Miner.Sse
{

    /// <summary>
    /// Factory for <see cref="SseMiner"/>.
    /// </summary>
    [MinerFactory]
    public class SseMinerFactory : CpuMinerFactory
    {

        /// <summary>
        /// Gets whether SSE is supported in the current environment.
        /// </summary>
        /// <returns></returns>
        static readonly bool hasSse = SseMinerUtils.Detect();

        /// <summary>
        /// Gets the available resources to be allocated to miners.
        /// </summary>
        public override IEnumerable<MinerResource> Resources
        {
            get { return hasSse ? base.Resources : Enumerable.Empty<MinerResource>(); }
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
