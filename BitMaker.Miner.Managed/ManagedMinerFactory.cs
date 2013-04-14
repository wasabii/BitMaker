using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

using BitMaker.Miner.Cpu;

namespace BitMaker.Miner.Managed
{

    /// <summary>
    /// Factory for <see cref="T:ManagedMiner"/>.
    /// </summary>
    [MinerFactory]
    public class ManagedMinerFactory : CpuMinerFactory
    {

        readonly IEnumerable<ManagedMiner> miners;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="context"></param>
        [ImportingConstructor]
        public ManagedMinerFactory([Import] IMinerContext context)
        {
            miners = Cpus
                .Select(i => new ManagedMiner(context, i))
                .ToList();
        }

        public override IEnumerable<IMiner> Miners
        {
            get { return miners; }
        }

    }

}
