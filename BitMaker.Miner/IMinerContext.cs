namespace BitMaker.Miner
{

    /// <summary>
    /// Context information available to plugins.
    /// </summary>
    public interface IMinerContext
    {

        /// <summary>
        /// Gets a new unit of work. Blocks until a unit of work is available. If no more units of work will be
        /// available, <c>null</c> will be returned.
        /// </summary>
        /// <returns></returns>
        Work GetWork(IMiner miner, string comment);

        /// <summary>
        /// Submits a finished unit of work. Blocks until the unit of work has been verified.
        /// </summary>
        /// <param name="work"></param>
        /// <returns><c>true</c> if the unit of work was a successful solution</returns>
        bool SubmitWork(IMiner miner, Work work, string comment);

        /// <summary>
        /// Reports that <paramref name="count"/> hashes have been generated. Invoke repeatidly to ensure hash statistics are maintained.
        /// </summary>
        /// <param name="plugin"><see cref="T:IPlugin"/> that was responsible for generation of the hashes</param>
        /// <param name="count">number of hashes to report</param>
        void ReportHashes(IMiner plugin, long count);

    }

}
