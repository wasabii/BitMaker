using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;

using BitMaker.Utils;

namespace BitMaker.Miner.Cpu
{

    [Miner]
    public class CpuMiner : IMiner
    {

        private static int THREAD_COUNT_MAX = 0;

        private readonly SHA256 sha256 = SHA256.Create();
        private readonly object syncRoot = new object();
        private bool started = false;
        private IMinerContext ctx;

        /// <summary>
        /// Set of available CPU solvers.
        /// </summary>
        [ImportMany]
        private IEnumerable<CpuSolver> Solvers { get; set; }

        /// <summary>
        /// <see cref="T:CpuSolver"/> implementation selected by speed.
        /// </summary>
        private CpuSolver solver;

        /// <summary>
        /// Signals work threads to stop.
        /// </summary>
        private CancellationTokenSource cts;

        /// <summary>
        /// Set of threads that attempt to pull and process work.
        /// </summary>
        private Thread[] workThreads;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public CpuMiner()
        {
            // import available plugins
            new CompositionContainer(new DirectoryCatalog(".", "*.dll")).SatisfyImportsOnce(this);
        }

        /// <summary>
        /// Starts the work threads.
        /// </summary>
        private void StartThreads(ParameterizedThreadStart threadStart, object state)
        {
            workThreads = new Thread[THREAD_COUNT_MAX == 0 ? Environment.ProcessorCount : THREAD_COUNT_MAX];
            for (int i = 0; i < workThreads.Length; i++)
            {
                workThreads[i] = new Thread(threadStart)
                {
                    Name = "CpuMiner Thread : " + i,
                    IsBackground = true,
                    Priority = ThreadPriority.Lowest,
                };
                workThreads[i].Start(state);
            }
        }

        /// <summary>
        /// Stops the work threads.
        /// </summary>
        /// <param name="cts"></param>
        private void StopThreads(CancellationTokenSource cts)
        {
            // signal threads to die
            if (cts != null)
                cts.Cancel();

            // wait for all threads to die
            foreach (var workThread in workThreads)
                workThread.Join();

            // clean up state
            workThreads = null;
        }

        public void Start(IMinerContext ctx)
        {
            lock (syncRoot)
            {
                if (started)
                    return;

                this.ctx = ctx;
                this.cts = new CancellationTokenSource();

                // choose the fastest solver implementation if not already done
                solver = solver ?? SelectSolver();
                if (solver == null)
                    throw new Exception("Could not select a solver implementation.");

                Console.WriteLine("Solver: {0}", solver.GetType().Name);

                // create work threads
                StartThreads(WorkThread, cts.Token);

                started = true;
            }
        }

        public void Stop()
        {
            lock (syncRoot)
            {
                if (!started)
                    return;

                // stop the work threads
                StopThreads(cts);

                cts = null;
                ctx = null;
                started = false;
            }
        }

        /// <summary>
        /// Reports hashes to the context.
        /// </summary>
        /// <param name="hashCount"></param>
        private void ReportHashes(uint hashCount)
        {
            ctx.ReportHashes(this, hashCount);
        }

        /// <summary>
        /// <see cref="T:ICpuSolverStatus"/> implementation for testing.
        /// </summary>
        private class TestCpuStatus : ICpuSolverStatus
        {

            private CancellationToken ct;

            /// <summary>
            /// Running sum of hashes produced by testing.
            /// </summary>
            private int hashCount = 0;

            /// <summary>
            /// Initializes a new instance.
            /// </summary>
            public TestCpuStatus(CancellationToken ct)
            {
                this.ct = ct;
            }

            public bool Check(uint hashCount)
            {
                // update stored has count
                Interlocked.Add(ref this.hashCount, (int)hashCount);

                // end tests after 10 seconds
                return !ct.IsCancellationRequested;
            }

            /// <summary>
            /// Gets the hash count resulting from the test.
            /// </summary>
            public uint HashCount
            {
                get { return (uint)hashCount; }
            }

        }

        /// <summary>
        /// Tests available implementations of <see cref="T:CpuSolver"/> and returns the fastest.
        /// </summary>
        private CpuSolver SelectSolver()
        {
            // record results of tests
            var results = new Dictionary<CpuSolver, uint>();

            // test each solver
            foreach (var solver in Solvers)
            {
                Console.WriteLine("Testing Solver: {0}", solver.GetType().Name);

                var cts = new CancellationTokenSource();
                var status = new TestCpuStatus(cts.Token);

                // begins a test of the solver
                StartThreads(TestWorkThread, new Tuple<CpuSolver, ICpuSolverStatus>(solver, status));

                // wait 5 seconds, read hash count immediately, and signal shutdown
                Thread.Sleep(5000);
                var hashCount = status.HashCount;

                // stop the threads and wait for them
                StopThreads(cts);

                // record number of hashes produced by solver
                results[solver] = hashCount;

                Console.WriteLine("{0}: {1} hashes", solver.GetType().Name, hashCount);
            }

            // return fastest solver
            return results.OrderByDescending(i => i.Value).FirstOrDefault().Key;
        }

        /// <summary>
        /// Entry point for the solver implementation selection threads.
        /// </summary>
        /// <param name="state"></param>
        private void TestWorkThread(object state)
        {
            // unpack objects passed into thread
            var state_ = (Tuple<CpuSolver, ICpuSolverStatus>)state;
            var solver = state_.Item1;
            var status = state_.Item2;

            // solved block
            var work = new Work()
            {
                BlockNumber = 0,
                Header = Memory.Decode("00000001d915b8fd2face61c6fe22ab76cad5f46c11cebab697dbd9e00000804000000008fe5f19cbdd55b40db93be7ef8ae249e0b21ec6e29c833b186404de0de205cc54e0022ac1a132185007d1adf"),
                Target = Memory.Decode("ffffffffffffffffffffffffffffffffffffffffffffffffffffffff00000000"),
            };

            unsafe
            {
                // reset nonce to immediately after solution, so it can never be solved
                fixed (byte* headerPtr = work.Header)
                    ((uint*)headerPtr)[19] = Memory.ReverseEndian(Memory.ReverseEndian(((uint*)headerPtr)[19]) + 1);
            }

            // start solver on work
            Solve(solver, work, status);
        }

        /// <summary>
        /// Implementation of <see cref="T:TCpuSolverStatus"/> for standard work items.
        /// </summary>
        private class CpuSolverStatus : ICpuSolverStatus
        {

            public CpuMiner miner;
            public Work work;

            /// <summary>
            /// Initializes a new instance.
            /// </summary>
            /// <param name="miner"></param>
            /// <param name="work"></param>
            public CpuSolverStatus(CpuMiner miner, Work work)
            {
                if (miner == null)
                    throw new ArgumentNullException("miner");
                if (work == null)
                    throw new ArgumentNullException("work");

                this.miner = miner;
                this.work = work;
            }

            public bool Check(uint hashCount)
            {
                // report progress
                miner.ReportHashes(hashCount);

                // continue processing if work is up to date, and miner hasn't told us to stop
                return miner.ctx.CurrentBlockNumber == work.BlockNumber && !miner.cts.IsCancellationRequested;
            }

        }

        /// <summary>
        /// Entry point for a standard work thread.
        /// </summary>
        private void WorkThread(object state)
        {
            var ct = (CancellationToken)state;

            try
            {
                // continue working until canceled
                Work work;
                while (!ct.IsCancellationRequested)
                    if (Solve(solver, work = ctx.GetWork(this, solver.GetType().Name), new CpuSolverStatus(this, work)))
                        // solution found!
                        ctx.SubmitWork(this, work, solver.GetType().Name);
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
        }

        /// <summary>
        /// Prepares the buffers for processing a work item.
        /// </summary>
        /// <param name="work"></param>
        /// <param name="round1Blocks"></param>
        /// <param name="round1State"></param>
        /// <param name="round2Blocks"></param>
        /// <param name="round2State"></param>
        private unsafe void PrepareWork(Work work, out byte[] round1Blocks, out uint[] round1State, out byte[] round2Blocks, out uint[] round2State)
        {
            // allocate buffers to hold hashing work
            round1Blocks = Sha256.AllocateInputBuffer(80);
            round1State = Sha256.AllocateStateBuffer();
            round2Blocks = Sha256.AllocateInputBuffer(Sha256.SHA256_HASH_SIZE);
            round2State = Sha256.AllocateStateBuffer();

            fixed (byte* round1BlocksPtr = round1Blocks, round2BlocksPtr = round2Blocks)
            fixed (uint* round1StatePtr = round1State, round2StatePtr = round2State)
            {
                // header arrives in big endian, convert to host
                fixed (byte* workHeaderPtr = work.Header)
                    Memory.ReverseEndian((uint*)workHeaderPtr, (uint*)round1BlocksPtr, 20);

                // append '1' bit and trailing length
                Sha256.Prepare(round1BlocksPtr, 80, 0);
                Sha256.Prepare(round1BlocksPtr + Sha256.SHA256_BLOCK_SIZE, 80, 1);

                // hash first half of header
                Sha256.Initialize(round1StatePtr);
                Sha256.Transform(round1StatePtr, round1BlocksPtr);

                // initialize values for round 2
                Sha256.Initialize(round2StatePtr);
                Sha256.Prepare(round2BlocksPtr, Sha256.SHA256_HASH_SIZE, 0);
            }
        }

        /// <summary>
        /// Attempts to solve the given work with the specified solver. Returns <c>true</c> if a solution is found.
        /// <paramref name="work"/> is updated to reflect the solution.
        /// </summary>
        /// <param name="work"></param>
        private unsafe bool Solve(CpuSolver solver, Work work, ICpuSolverStatus status)
        {
            // allocate buffers to hold hashing work
            byte[] round1Blocks, round2Blocks;
            uint[] round1State, round2State;

            // allocate buffers
            PrepareWork(work, out round1Blocks, out round1State, out round2Blocks, out round2State);

            fixed (byte* round1BlocksPtr = round1Blocks, round2BlocksPtr = round2Blocks)
            fixed (uint* round1StatePtr = round1State, round2StatePtr = round2State)
            {
                // enable this in a bit, and feed it the results of the CPU miner, so we can check them against each other
                uint? nonce = solver.Solve(work, status, round1StatePtr, round1BlocksPtr + Sha256.SHA256_BLOCK_SIZE, round2StatePtr, round2BlocksPtr);

                // solution found!
                if (nonce != null)
                {
                    // replace header data on work
                    fixed (byte* headerPtr = work.Header)
                        ((uint*)headerPtr)[19] = Memory.ReverseEndian((uint)nonce);

                    // let the caller know
                    return true;
                }
            }

            // no solution
            return false;
        }

        /// <summary>
        /// Allows a solver to periodically check in and report hashes.
        /// </summary>
        /// <param name="blockNumber"></param>
        /// <param name="hashes"></param>
        /// <returns></returns>
        public bool Check(uint blockNumber, uint hashes)
        {
            ctx.ReportHashes(this, hashes);
            return ctx.CurrentBlockNumber == blockNumber && !cts.IsCancellationRequested;
        }

    }

}
