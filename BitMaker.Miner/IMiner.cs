namespace BitMaker.Miner
{

    /// <summary>
    /// Interface required by miner implementations.
    /// </summary>
    public interface IMiner
    {

        /// <summary>
        /// Gets the device consumed by the miner.
        /// </summary>
        MinerDevice Device { get; }

        /// <summary>
        /// Starts execution of the miner.
        /// </summary>
        void Start();

        /// <summary>
        /// Stops execution of the miner.
        /// </summary>
        void Stop();

    }

}
