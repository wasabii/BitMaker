namespace BitMaker.Miner.Plugin
{

    /// <summary>
    /// Structured representation of a block header.
    /// </summary>
    public class BlockHeader
    {

        public uint Version { get; set; }

        public byte[] PreviousHash { get; set; }

        public byte[] MerkleRoot { get; set; }

        public uint Timestamp { get; set; }

        public uint Difficulty { get; set; }

        public uint Nonce { get; set; }

    }

}
