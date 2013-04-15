using System;
using System.Text;
using System.Threading;

using Cloo;

using BitMaker.Utils;

namespace BitMaker.Miner.Gpu
{

    /// <summary>
    /// CPU miner base class which provides a place to plug in the search algorithm.
    /// </summary>
    public sealed class GpuMiner : IMiner
    {

        readonly object syncRoot = new object();

        /// <summary>
        /// Context under which work is done.
        /// </summary>
        public IMinerContext Context { get; private set; }

        /// <summary>
        /// GPU we are bound to.
        /// </summary>
        public GpuDevice Gpu { get; private set; }

        /// <summary>
        /// Signals processes to halt.
        /// </summary>
        private CancellationTokenSource cts;

        /// <summary>
        /// Gets a reference to a token used to signal that processing should halt.
        /// </summary>
        public CancellationToken CancellationToken
        {
            get { return cts.Token; }
        }

        /// <summary>
        /// Gets the GPU consumed by this miner.
        /// </summary>
        public MinerDevice Device
        {
            get { return Gpu; }
        }

        /// <summary>
        /// Thread that pulls and processes work.
        /// </summary>
        Thread workThread;

        ComputeContext clContext;
        ComputeDevice clDevice;
        ComputeProgram clProgram;
        ComputeCommandQueue clQueue;
        ComputeKernel clKernel;
        ComputeBuffer<uint> clBuffer0;
        ComputeBuffer<uint> clBuffer1;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="gpu"></param>
        public GpuMiner(IMinerContext context, GpuDevice gpu)
        {
            Context = context;
            Gpu = gpu;
        }

        /// <summary>
        /// Starts a single thread to pull and process work.
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="resource"></param>
        public void Start()
        {
            lock (syncRoot)
            {
                if (workThread != null)
                    return;

                cts = new CancellationTokenSource();

                workThread = new Thread(WorkThread)
                {
                    Name = GetType().Name + " : " + Gpu.Id,
                    IsBackground = true,
                    Priority = ThreadPriority.Lowest,
                };
                workThread.Start();
            }
        }

        /// <summary>
        /// Stops the miner from running.
        /// </summary>
        public void Stop()
        {
            lock (syncRoot)
            {
                if (workThread == null)
                    return;

                // signal thread to die
                if (cts != null)
                    cts.Cancel();

                // wait for thread to die
                workThread.Join();

                // clean up state
                workThread = null;
                cts = null;
            }
        }

        /// <summary>
        /// Entry point for a standard work thread.
        /// </summary>
        void WorkThread()
        {
            InitializeOpenCL();

            try
            {
                // continue working until canceled
                while (!cts.IsCancellationRequested)
                    Work(Context.GetWork(this, GetType().Name));
            }
            catch (OperationCanceledException)
            {
                // ignore
            }

            clQueue.Finish();

            clKernel.Dispose();
            clKernel = null;

            clBuffer0.Dispose();
            clBuffer0 = null;

            clBuffer1.Dispose();
            clBuffer1 = null;

            clQueue.Dispose();
            clQueue = null;

            clProgram.Dispose();
            clProgram = null;

            clContext.Dispose();
            clContext = null;

            clDevice = null;
        }

        /// <summary>
        /// Prepares the buffers for processing a work item.
        /// </summary>
        /// <param name="work"></param>
        /// <param name="round1Blocks"></param>
        /// <param name="round1State"></param>
        /// <param name="round2Blocks"></param>
        /// <param name="round2State"></param>
        unsafe void PrepareWork(Work work, out byte[] round1Blocks, out uint[] round1State, out byte[] round2Blocks, out uint[] round2State)
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
        /// Attempts to initialize OpenCL for the selected GPU.
        /// </summary>
        void InitializeOpenCL()
        {
            // only initialize once
            if (clKernel != null)
                return;

            clDevice = Gpu.CLDevice;

            // context we'll be working underneath
            clContext = new ComputeContext(
                new[] { clDevice },
                new ComputeContextPropertyList(clDevice.Platform),
                null,
                IntPtr.Zero);

            // queue to control device
            clQueue = new ComputeCommandQueue(clContext, clDevice, ComputeCommandQueueFlags.None);

            // buffers to store kernel output
            clBuffer0 = new ComputeBuffer<uint>(clContext, ComputeMemoryFlags.ReadOnly, 16);
            clBuffer1 = new ComputeBuffer<uint>(clContext, ComputeMemoryFlags.ReadOnly, 16);

            // obtain the program
            clProgram = new ComputeProgram(clContext, Gpu.GetSource());

            var b = new StringBuilder();
            if (Gpu.WorkSize > 0)
                b.Append(" -D WORKSIZE=").Append(Gpu.WorkSize);
            if (Gpu.HasBitAlign)
                b.Append(" -D BITALIGN");
            if (Gpu.HasBfiInt)
                b.Append(" -D BFIINT");

            try
            {
                // build kernel for device
                clProgram.Build(new[] { clDevice }, b.ToString(), null, IntPtr.Zero);
            }
            catch (ComputeException)
            {
                throw new Exception(clProgram.GetBuildLog(clDevice));
            }

            clKernel = clProgram.CreateKernel("search");
        }

        /// <summary>
        /// Reports progress and checks for whether we should terminate the current work item.
        /// </summary>
        /// <param name="work"></param>
        /// <param name="?"></param>
        /// <returns></returns>
        bool Progress(Work work, long hashes)
        {
            // report hashes to context
            Context.ReportHashes(this, hashes);

            // abort if we are working on stale work, or if instructed to
            return 
                work.Pool.CurrentBlockNumber == work.BlockNumber &&
                !CancellationToken.IsCancellationRequested;
        }

        /// <summary>
        /// Attempts to solve the given work with the specified solver. Returns <c>true</c> if a solution is found.
        /// <paramref name="work"/> is updated to reflect the solution.
        /// </summary>
        /// <param name="work"></param>
        unsafe void Work(Work work)
        {
            // allocate buffers to hold hashing work
            byte[] round1Blocks, round2Blocks;
            uint[] round1State, round1State2Pre, round2State;

            // allocate buffers and create partial hash
            PrepareWork(work, out round1Blocks, out round1State, out round2Blocks, out round2State);

            // build message schedule without nonce
            uint* W = stackalloc uint[64];
            fixed (byte* round1BlocksPtr = round1Blocks)
                Sha256.Schedule(round1BlocksPtr + Sha256.SHA256_BLOCK_SIZE, W);

            // complete first three rounds of block 2
            round1State2Pre = Sha256.AllocateStateBuffer();
            Array.Copy(round1State, round1State2Pre, Sha256.SHA256_STATE_SIZE);
            Sha256.Round(ref round1State2Pre[0], ref round1State2Pre[1], ref round1State2Pre[2], ref round1State2Pre[3], ref round1State2Pre[4], ref round1State2Pre[5], ref round1State2Pre[6], ref round1State2Pre[7], W, 0);
            Sha256.Round(ref round1State2Pre[0], ref round1State2Pre[1], ref round1State2Pre[2], ref round1State2Pre[3], ref round1State2Pre[4], ref round1State2Pre[5], ref round1State2Pre[6], ref round1State2Pre[7], W, 1);
            Sha256.Round(ref round1State2Pre[0], ref round1State2Pre[1], ref round1State2Pre[2], ref round1State2Pre[3], ref round1State2Pre[4], ref round1State2Pre[5], ref round1State2Pre[6], ref round1State2Pre[7], W, 2);

            // precalculated peices that are independent of nonce
            uint W16 = W[16];
            uint W17 = W[17];
            uint W18 = W[18];
            uint W19 = W[19];
            uint W31 = W[31];
            uint W32 = W[32];
            uint PreVal4 = round1State[4] + (Sha256.Rotr(round1State2Pre[1], 6) ^ Sha256.Rotr(round1State2Pre[1], 11) ^ Sha256.Rotr(round1State2Pre[1], 25)) + (round1State2Pre[3] ^ (round1State2Pre[1] & (round1State2Pre[2] ^ round1State2Pre[3]))) + Sha256.K[3];
            uint T1 = (Sha256.Rotr(round1State2Pre[5], 2) ^ Sha256.Rotr(round1State2Pre[5], 13) ^ Sha256.Rotr(round1State2Pre[5], 22)) + ((round1State2Pre[5] & round1State2Pre[6]) | (round1State2Pre[7] & (round1State2Pre[5] | round1State2Pre[6])));
            uint PreVal4_state0 = PreVal4 + round1State[0];
            uint PreVal4_state0_k7 = (uint)(PreVal4_state0 + 0xAB1C5ED5L);
            uint PreVal4_T1 = PreVal4 + T1;
            uint B1_plus_K6 = (uint)(round1State2Pre[1] + 0x923f82a4L);
            uint C1_plus_K5 = (uint)(round1State2Pre[2] + 0x59f111f1L);
            uint W16_plus_K16 = (uint)(W16 + 0xe49b69c1L);
            uint W17_plus_K17 = (uint)(W17 + 0xefbe4786L);

            // clear output buffers, in case they've already been used
            uint[] outputZero = new uint[16];
            clQueue.WriteToBuffer(outputZero, clBuffer0, true, null);
            clQueue.WriteToBuffer(outputZero, clBuffer1, true, null);

            // to hold output buffer
            uint[] output = new uint[16];

            // swaps between true and false to allow a kernel to execute while testing output of last run
            bool outputAlt = true;

            // size of local work groups
            long localWorkSize = Gpu.WorkSize;

            // number of items to dispatch to GPU at a time
            long globalWorkSize = localWorkSize * localWorkSize * 8;

            // begin working at 0
            uint nonce = 0;

            // continue dispatching work to the GPU
            while (true)
            {
                // if one loop has completed
                if (nonce > 0)
                {
                    // read output into current output buffer then reset buffer
                    clQueue.ReadFromBuffer(outputAlt ? clBuffer0 : clBuffer1, ref output, true, null);

                    // scan output buffer for produced nonce values
                    fixed (uint* o = output)
                        for (int j = 0; j < 16; j++)
                            if (o[j] != 0)
                            {
                                // replace header data on work
                                fixed (byte* headerPtr = work.Header)
                                    ((uint*)headerPtr)[19] = output[j];

                                // submit work for validation
                                Context.SubmitWork(this, work, GetType().Name);

                                // clear output buffer
                                clQueue.WriteToBuffer(outputZero, outputAlt ? clBuffer0 : clBuffer1, true, null);
                            }
                }

                // execute kernel with computed values
                clQueue.Finish();
                clKernel.SetValueArgument(0, PreVal4_state0);
                clKernel.SetValueArgument(1, PreVal4_state0_k7);
                clKernel.SetValueArgument(2, PreVal4_T1);
                clKernel.SetValueArgument(3, W18);
                clKernel.SetValueArgument(4, W19);
                clKernel.SetValueArgument(5, W16);
                clKernel.SetValueArgument(6, W17);
                clKernel.SetValueArgument(7, W16_plus_K16);
                clKernel.SetValueArgument(8, W17_plus_K17);
                clKernel.SetValueArgument(9, W31);
                clKernel.SetValueArgument(10, W32);
                clKernel.SetValueArgument(11, (uint)(round1State2Pre[3] + 0xB956c25bL));
                clKernel.SetValueArgument(12, round1State2Pre[1]);
                clKernel.SetValueArgument(13, round1State2Pre[2]);
                clKernel.SetValueArgument(14, round1State2Pre[7]);
                clKernel.SetValueArgument(15, round1State2Pre[5]);
                clKernel.SetValueArgument(16, round1State2Pre[6]);
                clKernel.SetValueArgument(17, C1_plus_K5);
                clKernel.SetValueArgument(18, B1_plus_K6);
                clKernel.SetValueArgument(19, round1State[0]);
                clKernel.SetValueArgument(20, round1State[1]);
                clKernel.SetValueArgument(21, round1State[2]);
                clKernel.SetValueArgument(22, round1State[3]);
                clKernel.SetValueArgument(23, round1State[4]);
                clKernel.SetValueArgument(24, round1State[5]);
                clKernel.SetValueArgument(25, round1State[6]);
                clKernel.SetValueArgument(26, round1State[7]);
                clKernel.SetMemoryArgument(27, outputAlt ? clBuffer0 : clBuffer1);
                clQueue.Execute(clKernel, null, new long[] { globalWorkSize }, new long[] { localWorkSize }, null);

                // report that we just hashed the work size number of hashes
                if (!Progress(work, globalWorkSize))
                    break;

                // update nonce and check whether it is now less than the work size, which indicates it overflowed
                if ((nonce += (uint)globalWorkSize) < (uint)globalWorkSize)
                    break;

                // next loop deals with other output buffer
                outputAlt = !outputAlt;
            }
        }

    }

}
