using System.Runtime.InteropServices;

namespace BitMaker.Utils
{

    /// <summary>
    /// Provides a window by which to view a block header.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size=80)]
    public unsafe struct BlockHeaderWindow
    {

        [FieldOffset(0)]
        public uint Version;
        
        [FieldOffset(4)]
        public fixed byte PreviousHash[32];

        [FieldOffset(32)]
        public fixed byte MerkleRoot[32];
        
        [FieldOffset(68)]
        public uint Timestamp;
        
        [FieldOffset(72)]
        public uint Difficulty;
        
        [FieldOffset(76)]
        public uint Nonce;

    }

}
