using System.Collections.Generic;
using System.Linq;

using BitMaker.Miner.Cpu;
using BitMaker.Utils.Native;

namespace BitMaker.Miner.Avx
{

    /// <summary>
    /// Factory for <see cref="AvxMiner"/>.
    /// </summary>
    [MinerFactory]
    public class AvxMinerFactory : CpuMinerFactory
    {

        /// <summary>
        /// Gets whether AVX is supported in the current environment.
        /// </summary>
        /// <returns></returns>
        static readonly bool hasAvx = AvxMinerUtils.Detect();

        /// <summary>
        /// Gets the available resources to be allocated to miners.
        /// </summary>
        public override IEnumerable<MinerResource> Resources
        {
            get { return hasAvx ? base.Resources : Enumerable.Empty<MinerResource>(); }
        }

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
