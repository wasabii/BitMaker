using System.Collections.Generic;

namespace BitMaker.Miner
{

    /// <summary>
    /// Exposes a miner implementation to the host.
    /// </summary>
    public interface IMinerFactory
    {

        /// <summary>
        /// Returns the set of resources that can be consumed by the miner implementation of this factory.
        /// </summary>
        IEnumerable<MinerResource> Resources { get; }

        /// <summary>
        /// Starts a new miner that consumes the given resource.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="resource"></param>
        /// <returns></returns>
        IMiner StartMiner(IMinerContext context, MinerResource resource);

    }

}
