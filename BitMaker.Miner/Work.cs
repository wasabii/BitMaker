using BitMaker.Utils;

namespace BitMaker.Miner
{

    /// <summary>
    /// Unit of work delivered to a plugin. Plugins have exclusive access to this work and can modify the header value
    /// before submitting it back to the context.
    /// </summary>
    public sealed class Work
    {

        /// <summary>
        /// Current block number when this work was delivered.
        /// </summary>
        public uint BlockNumber { get; set; }

        /// <summary>
        /// Gets or sets the header data.
        /// </summary>
        public byte[] Header { get; set; }

        /// <summary>
        /// Gets or sets the target data.
        /// </summary>
        public byte[] Target { get; set; }

        /// <summary>
        /// Ensures the current header generates a hash that matches the target difficulty.
        /// </summary>
        /// <returns></returns>
        public unsafe bool Validate()
        {
            // allocate buffers to hold hashing work
            byte[] round1Blocks = Sha256.AllocateInputBuffer(80);
            uint[] round1State = Sha256.AllocateStateBuffer();
            byte[] round2Blocks = Sha256.AllocateInputBuffer(Sha256.SHA256_HASH_SIZE);
            uint[] round2State = Sha256.AllocateStateBuffer();
            byte[] hash = Sha256.AllocateHashBuffer();
            
            fixed (byte* round1BlocksPtr = round1Blocks, round2BlocksPtr = round2Blocks, hashPtr = hash, targetPtr = Target)
            fixed (uint* round1StatePtr = round1State, round2StatePtr = round2State)
            {
                byte* round1Block1Ptr = round1BlocksPtr;
                byte* round1Block2Ptr = round1BlocksPtr + Sha256.SHA256_BLOCK_SIZE;
                byte* round2Block1Ptr = round2BlocksPtr;

                // header arrives in big endian, convert to host
                fixed (byte* workHeaderPtr = Header)
                    Memory.ReverseEndian((uint*)workHeaderPtr, (uint*)round1BlocksPtr, 20);

                // prepare states and blocks
                Sha256.Initialize(round1StatePtr);
                Sha256.Initialize(round2StatePtr);
                Sha256.Prepare(round1Block1Ptr, 80, 0);
                Sha256.Prepare(round1Block2Ptr, 80, 1);
                Sha256.Prepare(round2BlocksPtr, Sha256.SHA256_HASH_SIZE, 0);

                // hash first half of header
                Sha256.Transform(round1StatePtr, round1Block1Ptr);
                Sha256.Transform(round1StatePtr, round1Block2Ptr, (uint*)round2Block1Ptr);
                Sha256.Transform(round2StatePtr, round2Block1Ptr);
                Sha256.Finalize(round2StatePtr, hashPtr);

                // check final hash
                for (int i = 7; i >= 0; i--)
                    if (((uint*)hashPtr)[i] > ((uint*)targetPtr)[i])
                        return false;

                return true;
            }
        }


    }

}
