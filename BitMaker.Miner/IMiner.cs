namespace BitMaker.Miner
{

    /// <summary>
    /// Interface required by miner implementations.
    /// </summary>
    public interface IMiner
    {

        /// <summary>
        /// Stops execution of the miner.
        /// </summary>
        void Stop();

    }

}
