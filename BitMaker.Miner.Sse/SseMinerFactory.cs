using System.Collections.Generic;
using System.ComponentModel.Composition;
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
        /// Available miners.
        /// </summary>
        readonly IEnumerable<SseMiner> miners;

        /// <summary>
        /// Gets the available resources to be allocated to miners.
        /// </summary>
        public override IEnumerable<MinerDevice> Devices
        {
            get { return hasSse ? base.Devices : Enumerable.Empty<MinerDevice>(); }
        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="context"></param>
        [ImportingConstructor]
        public SseMinerFactory([Import] IMinerContext context)
        {
            miners = Cpus
                .Where(i => hasSse)
                .Select(i => new SseMiner(context, i))
                .ToList();
        }

        public override IEnumerable<IMiner> Miners
        {
            get { return miners; }
        }

    }

}
