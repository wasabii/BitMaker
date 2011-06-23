using System;
using System.Threading;

using BitMaker.Utils;
using System.Security.Cryptography;

namespace BitMaker.Miner.Plugin.Cpu
{

    [Miner]
    public class CpuPlugin : IMiner
    {

        private readonly SHA256 sha256 = SHA256.Create();
        private readonly object syncRoot = new object();
        private bool started = false;
        private IMinerContext ctx;

        /// <summary>
        /// Signals work threads to stop.
        /// </summary>
        private CancellationTokenSource cts;

        /// <summary>
        /// Set of threads that attempt to pull and process work.
        /// </summary>
        private Thread[] workThreads;

        public void Start(IMinerContext ctx)
        {
            lock (syncRoot)
            {
                if (started)
                    return;

                this.ctx = ctx;
                this.cts = new CancellationTokenSource();

                // create work threads
                workThreads = new Thread[1];
                workThreads = new Thread[Environment.ProcessorCount + 1];
                for (int i = 0; i < workThreads.Length; i++)
                {
                    workThreads[i] = new Thread(WorkThread)
                    {
                        Name = "Cpu Work Thread : " + i,
                        IsBackground = true,
                        Priority = ThreadPriority.Lowest,
                    };
                    workThreads[i].Start(cts.Token);
                }

                started = true;
            }
        }

        public void Stop()
        {
            lock (syncRoot)
            {
                if (!started)
                    return;

                // signal threads to die
                cts.Cancel();

                // wait for all threads to die
                foreach (var workThread in workThreads)
                    workThread.Join();

                // clean up state
                workThreads = null;
                cts = null;
                ctx = null;
                started = false;
            }
        }

        /// <summary>
        /// Entry-point for main thread. Uses PLinq to schedule all available work.
        /// </summary>
        private void WorkThread(object state)
        {
            var ct = (CancellationToken)state;

            try
            {
                while (!ct.IsCancellationRequested)
                    Work(ctx.GetWork());
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
        }

        /// <summary>
        /// Returns true if the plugin should shutdown.
        /// </summary>
        internal bool IsCancellationRequested
        {
            get { return cts.IsCancellationRequested; }
        }

        /// <summary>
        /// Reports a hash count to the miner.
        /// </summary>
        /// <param name="hashCount"></param>
        internal void ReportHashes(int hashCount)
        {
            ctx.ReportHashes(this, hashCount);
        }

        /// <summary>
        /// Gets the current block number.
        /// </summary>
        internal uint CurrentBlockNumber
        {
            get { return ctx.CurrentBlockNumber; }
        }

        /// <summary>
        /// Invoked for each retrieved work item.
        /// </summary>
        /// <param name="work"></param>
        private unsafe void Work(Work work)
        {
            // starting number so we can detect when it changes
            var currentBlockNumber = ctx.CurrentBlockNumber;

            /* This procedure is optimized based on the internals of the SHA-256 algorithm. As each block is transformed,
             * the state variable is updated. Finalizing the hash consists of reversing the byte order of the state.
             * Data to be hashed needs to have it's byte order reversed. Instead of reversing the first state to obtain
             * the first hash and then reversing it again, we output the transform of the header directly into a block
             * pre-padded to the size of a hash, and then transform that again using new state. This prevents the double
             * byte order swap.
             **/

            // bail out on stale or no work
            if (work == null || work.CancellationToken.IsCancellationRequested || cts.IsCancellationRequested)
                return;

            // allocate buffers to hold hashing work
            byte[] round1Blocks = Sha256.AllocateInputBuffer(80);
            uint[] round1State = Sha256.AllocateStateBuffer();
            byte[] round2Blocks = Sha256.AllocateInputBuffer(Sha256.SHA256_HASH_SIZE);
            uint[] round2State = Sha256.AllocateStateBuffer();

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

                // solve the header
                uint? nonce = new ManagedHasher().Solve(this, work, round1StatePtr, round1BlocksPtr, round2StatePtr, round2BlocksPtr);

                // solution found!
                if (nonce != null)
                {
                    // ensure nonce is set on header
                    ((uint*)round1BlocksPtr)[19] = Memory.ReverseEndian((uint)nonce);

                    // replace header data on work
                    work.Header = new byte[80];
                    fixed (byte* dstHeaderPtr = work.Header)
                        Memory.Copy((uint*)round1BlocksPtr, (uint*)dstHeaderPtr, 20);

                    // compute full hash of header using native implementation
                    var hash = Memory.Encode(sha256.ComputeHash(sha256.ComputeHash(work.Header)));

                    // display message indicating submission
                    Console.WriteLine();
                    Console.WriteLine();
                    Console.WriteLine("SOLUTION: {0}", hash);

                    // submit work for completion
                    if (ctx.SubmitWork(work))
                        Console.WriteLine("ACCEPTED: {0}", hash);
                    else
                        Console.WriteLine("REJECTED: {0}", hash);

                    Console.WriteLine();
                    Console.WriteLine();
                }
            }
        }

    }

}
