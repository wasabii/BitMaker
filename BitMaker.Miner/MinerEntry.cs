namespace BitMaker.Miner
{

    public sealed class MinerEntry
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="miner"></param>
        /// <param name="resource"></param>
        internal MinerEntry(IMiner miner, MinerDevice resource)
        {
            Miner = miner;
            Resource = resource;
        }

        /// <summary>
        /// Currently executing miner.
        /// </summary>
        public IMiner Miner { get; private set; }

        /// <summary>
        /// Resource miner is consuming.
        /// </summary>
        public MinerDevice Resource { get; private set; }

    }
}
