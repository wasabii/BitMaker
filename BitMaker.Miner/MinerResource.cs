namespace BitMaker.Miner
{

    /// <summary>
    /// Represents a resource on the system that can be utilized by a miner.
    /// </summary>
    public abstract class MinerResource
    {

        /// <summary>
        /// Unique identifier key of the resource.
        /// </summary>
        public string Id { get; set; }

    }

}
