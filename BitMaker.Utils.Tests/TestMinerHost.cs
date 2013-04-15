using System;
using BitMaker.Miner;

namespace BitMaker.Utils.Tests
{

    /// <summary>
    /// Miner host implementation for testing purposes.
    /// </summary>
    public class TestMinerHost : IMinerContext
    {

        Func<IMiner, Work, bool> onWork;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="onWork"></param>
        public TestMinerHost(Func<IMiner, Work, bool> onWork)
        {
            this.onWork = onWork;
        }

        public Work GetWork(IMiner miner, string comment)
        {
            return Utils.Work;
        }

        public bool SubmitWork(IMiner miner, Work work, string comment)
        {
            return onWork(miner, work);
        }

        public void ReportHashes(IMiner plugin, long count)
        {

        }

    }

}
