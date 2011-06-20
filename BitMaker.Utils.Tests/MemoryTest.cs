using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMaker.Utils.Tests
{

    [TestClass()]
    public class MemoryTest
    {

        [TestMethod()]
        public void ReverseEndianTestUInt32()
        {
            uint value = 63335;
            uint expected = 1744240640;
            uint actual;
            actual = Memory.ReverseEndian(value);
            Assert.AreEqual(expected, actual);
        }

        [TestMethod()]
        public void ReverseEndianTestUInt64()
        {
            ulong value = 63335;
            ulong expected = 7491456505154109440;
            ulong actual;
            actual = Memory.ReverseEndian(value);
            Assert.AreEqual(expected, actual);
        }

    }

}
