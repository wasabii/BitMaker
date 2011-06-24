using BitMaker.Utils;

namespace BitMaker.Miner.Cpu
{

    [CpuSolver]
    public class ManagedCpuSolver : CpuSolver
    {

        public override unsafe uint? Solve(Work work, ICpuSolverStatus status, uint* round1State, byte* round1Block2, uint* round2State, byte* round2Block1)
        {
            // start at existing nonce
            uint nonce = Memory.ReverseEndian(((uint*)round1Block2)[3]);

            uint* round2State2 = stackalloc uint[Sha256.SHA256_STATE_SIZE];

            uint count = 0;
            for (; ; )
            {
                // transform variable second half of block using saved state from first block, into pre-padded round 2 block (end of first hash)
                Sha256.Transform(round1State, round1Block2, (uint*)round2Block1);

                // transform round 2 block into round 2 state (second hash)
                Sha256.Transform(round2State, round2Block1, round2State2);

                // test for potentially valid hash
                if (round2State2[7] == 0U)
                    return nonce;

                // update the nonce value
                ((uint*)round1Block2)[3] = Memory.ReverseEndian(++nonce);

                // at the end of our nonce values, we can't continue
                if (nonce == uint.MaxValue)
                    break;

                // only report and check for exit conditions every so often
                if ((++count % 65536) == 0)
                    if (!status.Check(65536))
                        break;
            }

            return null;
        }

    }

}
