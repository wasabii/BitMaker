namespace BitMaker.Miner
{

    /// <summary>
    /// Represents a device on the system that can be utilized by a miner.
    /// </summary>
    public abstract class MinerDevice
    {

        /// <summary>
        /// Unique identifier key of the resource.
        /// </summary>
        public virtual string Id { get; set; }

    }

}
