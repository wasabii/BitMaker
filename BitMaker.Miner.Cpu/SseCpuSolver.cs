using BitMaker.Utils.Native;
using System.Management;

namespace BitMaker.Miner.Cpu
{

    [CpuSolver]
    public class SseCpuSolver : CpuSolver
    {

        public override unsafe uint? Solve(Work work, ICpuSolverStatus status, uint* round1State, byte* round1Block2, uint* round2State, byte* round2Block1)
        {
            // invoked by the SSE solver periodically
            var statusFunc = (StatusDelegate)(i => status.Check(i));

            // implemention lives in mixed mode assembly
            return BitMaker.Utils.Native.SseCpuHasher.Solve(round1State, round1Block2, round2State, round2Block1, statusFunc);
        }

    }

}
