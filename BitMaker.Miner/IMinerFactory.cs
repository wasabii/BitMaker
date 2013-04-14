using System.Collections.Generic;

namespace BitMaker.Miner
{

    /// <summary>
    /// Exposes a miner implementation to the host.
    /// </summary>
    public interface IMinerFactory
    {

        /// <summary>
        /// Gets the set of miners exposed by this factory.
        /// </summary>
        IEnumerable<IMiner> Miners { get; }

    }

}
