namespace BitMaker.Miner.Cpu
{

    /// <summary>
    /// Implements a CPU-bound algorithm to search the nonce space for a value to create a block. 
    /// </summary>
    public abstract class CpuSolver
    {

        public abstract unsafe uint? Solve(CpuMiner cpu, Work work, uint* round1State, byte* round1Block2, uint* round2State, byte* round2Block1);

    }

}
