using BitMaker.Miner;

namespace BitMaker.Utils.Tests
{

    public static class Utils
    {

        // sample work with immediate solution
        public static readonly Work Work = new Work()
        {
            BlockNumber = 0,
            Header = Memory.Decode("00000001d915b8fd2face61c6fe22ab76cad5f46c11cebab697dbd9e00000804000000008fe5f19cbdd55b40db93be7ef8ae249e0b21ec6e29c833b186404de0de205cc54e0022ac1a132185007d1adf000000800000000000000000000000000000000000000000000000000000000000000000000000000000000080020000"),
            Target = Memory.Decode("ffffffffffffffffffffffffffffffffffffffffffffffffffffffff00000000"),
        };

    }

}
