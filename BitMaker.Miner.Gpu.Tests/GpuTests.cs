using System;
using System.Linq;
using BitMaker.Utils.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMaker.Miner.Gpu.Tests
{

    [TestClass]
    public class GpuTests
    {

        [TestMethod]
        public void TestGpu1()
        {
            var h = new TestMinerHost((miner, work) =>
            {
                return true;
            });
            var f = new GpuMinerFactory(h);
            var m = (GpuMiner)f.Miners.First();

            m.InitializeOpenCL();
            m.Work(h.GetWork(m, null));
        }

    }

}
