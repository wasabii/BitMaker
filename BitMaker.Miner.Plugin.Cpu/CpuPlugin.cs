using System;
using System.Threading;

using BitMaker.Utils;

namespace BitMaker.Miner.Plugin.Cpu
{

    [Plugin]
    public class CpuPlugin : IPlugin
    {

        private readonly object syncRoot = new object();
        private bool started = false;
        private IPluginContext ctx;

        /// <summary>
        /// Signals work threads to stop.
        /// </summary>
        private CancellationTokenSource cts;

        /// <summary>
        /// Set of threads that attempt to pull and process work.
        /// </summary>
        private Thread[] workThreads;

        public void Start(IPluginContext ctx)
        {
            lock (syncRoot)
            {
                if (started)
                    return;

                this.ctx = ctx;
                this.cts = new CancellationTokenSource();

                // create work threads
                workThreads = new Thread[Environment.ProcessorCount * 2];
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
        /// Invoked for each retrieved work item.
        /// </summary>
        /// <param name="work"></param>
        private unsafe void Work(Work work)
        {
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
            byte[] data = Sha256.AllocateInputBuffer(80);
            uint[] midstate = Sha256.AllocateStateBuffer();
            uint[] state = Sha256.AllocateStateBuffer();
            uint[] state2 = Sha256.AllocateStateBuffer();
            byte[] hash = new byte[Sha256.SHA256_BLOCK_SIZE];

            fixed (byte* workHeaderPtr = work.Header, workTargetPtr = work.Target)
            fixed (byte* dataPtr = data, hashPtr = hash)
            fixed (uint* midstatePtr = midstate, statePtr = state, state2Ptr = state2)
            {
                if (BitConverter.IsLittleEndian)
                    // header arrives in big endian, convert to host
                    Memory.ReverseEndian((uint*)workHeaderPtr, (uint*)dataPtr, 20);
                else
                    // simply copy if conversion not required
                    Memory.Copy((uint*)workHeaderPtr, (uint*)dataPtr, 20);

                // append '1' bit and trailing length
                Sha256.Prepare(dataPtr, 80, 0);
                Sha256.Prepare(dataPtr + Sha256.SHA256_BLOCK_SIZE, 80, 1);

                // hash first half of header
                Sha256.Initialize(midstatePtr);
                Sha256.Transform(midstatePtr, dataPtr);

                // prepare the block of the hash buffer for the second round, this data shouldn't be overwritten
                Sha256.Prepare(hashPtr, Sha256.SHA256_HASH_SIZE, 0);

                // read initial nonce value
                uint nonce = Memory.ReverseEndian(((uint*)dataPtr)[19]);

                // initial state
                Sha256.Initialize(statePtr);

                // test every possible nonce value
                while (nonce <= uint.MaxValue)
                {
                    // transform variable second half of block using saved state
                    Sha256.Transform(midstatePtr, dataPtr + Sha256.SHA256_BLOCK_SIZE, (uint*)hashPtr);

                    // compute second hash back into hash
                    Sha256.Transform(statePtr, hashPtr, state2Ptr);

                    // only report and check for exit conditions every so often
                    if (nonce % 1024 == 0 && nonce > 0)
                    {
                        ctx.ReportHashes(this, 1024);
                        if (work.CancellationToken.IsCancellationRequested || cts.IsCancellationRequested)
                            break;
                    }

                    // the hash is byte order flipped state, quick check that state passes a test before doing work
                    if (state2Ptr[0] == 0U || state2Ptr[7] == 0U)
                    {
                        // replace header data on work
                        work.Header = new byte[80];
                        fixed (byte* dstHeaderPtr = work.Header)
                            Memory.Copy((uint*)dataPtr, (uint*)dstHeaderPtr, 20);

                        // finalize hash
                        byte[] finalHash = Sha256.AllocateHashBuffer();
                        fixed (byte* finalHashPtr = finalHash)
                            Sha256.Finalize(state2Ptr, finalHashPtr);

                        // encode for display purposes
                        var encodedHash = Memory.Encode(finalHash);

                        // display message indicating submission
                        Console.WriteLine();
                        Console.WriteLine();
                        Console.WriteLine("Submitting: {0}", encodedHash);

                        // submit work for completion
                        if (ctx.SubmitWork(work))
                            Console.WriteLine("Success: {0}", encodedHash);
                        else
                            Console.WriteLine("Failure: {0}", encodedHash);

                        Console.WriteLine();
                        Console.WriteLine();
                    }

                    // update the nonce value
                    ((uint*)dataPtr)[19] = Memory.ReverseEndian(++nonce);
                }
            }
        }

    }

}
