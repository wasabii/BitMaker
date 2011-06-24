namespace BitMaker.Miner.Cpu
{

    /// <summary>
    /// Provides an interface for an <see cref="T:CpuSolver"/> to report progress and check the status.
    /// </summary>
    public interface ICpuSolverStatus
    {

        /// <summary>
        /// Returns <c>true</c> if the solver should continue working. Accepts a hash count to report progress.
        /// </summary>
        /// <param name="hashCount"></param>
        /// <returns></returns>
        bool Check(uint hashCount);

    }

}
