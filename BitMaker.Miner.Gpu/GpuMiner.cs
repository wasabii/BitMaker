using System;
using System.Threading;
using System.Linq;

using BitMaker.Utils;
using Cloo;
using System.IO;

namespace BitMaker.Miner.Gpu
{

    /// <summary>
    /// CPU miner base class which provides a place to plug in the search algorithm.
    /// </summary>
    public sealed class GpuMiner : IMiner
    {

        private readonly object syncRoot = new object();

        /// <summary>
        /// Context under which work is done.
        /// </summary>
        public IMinerContext Context { get; private set; }

        /// <summary>
        /// GPU we are bound to.
        /// </summary>
        public GpuResource Gpu { get; private set; }

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
        /// Thread that pulls and processes work.
        /// </summary>
        private Thread workThread;

        private ComputeContext clContext;
        private ComputeDevice clDevice;
        private ComputeProgram clProgram;
        private ComputeCommandQueue clQueue;
        private ComputeKernel clKernel;
        private ComputeBuffer<uint> clBuffer0;
        private ComputeBuffer<uint> clBuffer1;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="gpu"></param>
        public GpuMiner(IMinerContext context, GpuResource gpu)
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
        private void WorkThread()
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

            clDevice = null;

            clProgram.Dispose();
            clProgram = null;

            clContext.Dispose();
            clContext = null;
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
        /// Attempts to initialize OpenCL for the selected GPU.
        /// </summary>
        private void InitializeOpenCL()
        {
            // only initialize once
            if (clKernel != null)
                return;

            // select the device we've been instructed to use
            clDevice = ComputePlatform.Platforms
                .SelectMany(i => i.Devices)
                .SingleOrDefault(i => i.Handle.Value == Gpu.CLDeviceHandle.Value);

            // context we'll be working underneath
            clContext = new ComputeContext(new ComputeDevice[] { clDevice }, new ComputeContextPropertyList(clDevice.Platform), null, IntPtr.Zero);

            // queue to control device
            clQueue = new ComputeCommandQueue(clContext, clDevice, ComputeCommandQueueFlags.None);

            // buffers to store kernel output
            clBuffer0 = new ComputeBuffer<uint>(clContext, ComputeMemoryFlags.ReadOnly, 16);
            clBuffer1 = new ComputeBuffer<uint>(clContext, ComputeMemoryFlags.ReadOnly, 16);

            // kernel code
            string kernelCode;
            using (var rdr = new StreamReader(GetType().Assembly.GetManifestResourceStream("BitMaker.Miner.Gpu.DiabloMiner.cl")))
                kernelCode = rdr.ReadToEnd();

            clProgram = new ComputeProgram(clContext, kernelCode);

            try
            {
                // build kernel for device
                clProgram.Build(new ComputeDevice[] { clDevice }, "-D WORKSIZE=" + clDevice.MaxWorkGroupSize, null, IntPtr.Zero);
            }
            catch (ComputeException)
            {
                throw new Exception(clProgram.GetBuildLog(clDevice));
            }

            clKernel = clProgram.CreateKernel("search");
        }

        /// <summary>
        /// Attempts to solve the given work with the specified solver. Returns <c>true</c> if a solution is found.
        /// <paramref name="work"/> is updated to reflect the solution.
        /// </summary>
        /// <param name="work"></param>
        private unsafe void Work(Work work)
        {
            // invoked periodically to report hashes and check status
            var check = (Func<uint, bool>)(i =>
            {
                // report hashes to context
                Context.ReportHashes(this, i);

                // abort if we are working on stale work, or if instructed to
                return Context.CurrentBlockNumber == work.BlockNumber && !CancellationToken.IsCancellationRequested;
            });

            // allocate buffers to hold hashing work
            byte[] round1Blocks, round2Blocks;
            uint[] round1State, round1State2Pre, round2State;

            // allocate buffers and create partial hash
            PrepareWork(work, out round1Blocks, out round1State, out round2Blocks, out round2State);

            // static values for work
            uint W16, W17, W18, W19, W31, W32, PreVal4, T1, PreVal4_plus_state0, PreVal4_plus_T1;

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
            W16 = W[16];
            W17 = W[17];
            W18 = W[18];
            W19 = W[19];
            W31 = W[31];
            W32 = W[32];
            PreVal4 = round1State[4] + Sha256.Sigma1(round1State2Pre[4]) + Sha256.Ch(round1State2Pre[4], round1State2Pre[5], round1State2Pre[6]) + Sha256.K[3];
            T1 = Sha256.Sigma0(round1State2Pre[0]) + Sha256.Maj(round1State2Pre[0], round1State2Pre[1], round1State2Pre[2]);
            PreVal4_plus_state0 = PreVal4 + round1State[0];
            PreVal4_plus_T1 = PreVal4 + T1;

            // clear output buffers, in case they've already been used
            uint[] outputZero = new uint[16];
            clQueue.WriteToBuffer(outputZero, clBuffer0, true, null);
            clQueue.WriteToBuffer(outputZero, clBuffer0, true, null);

            // to hold output buffer
            uint[] output = new uint[16];

            // swaps between true and false to allow a kernel to execute while testing output of last run
            bool outputAlt = true;

            // size of local work groups
            long localWorkSize = clDevice.MaxWorkGroupSize;

            // number of items to dispatch to GPU at a time
            long globalWorkSize = localWorkSize * localWorkSize * 8;

            // begin working at 0
            uint nonce = 0;

            // continue dispatching work to the GPU
            while (true)
            {
                // list of output events
                var events = new ComputeEventList();

                // read output into current output buffer then reset buffer
                clQueue.ReadFromBuffer(outputAlt ? clBuffer0 : clBuffer1, ref output, true, events);

                // scan output buffer for produced nonce values
                bool outputDirty = false;
                for (int j = 0; j < 16; j++)
                    if (output[j] != 0)
                    {
                        outputDirty = true;

                        // replace header data on work
                        fixed (byte* headerPtr = work.Header)
                            ((uint*)headerPtr)[19] = output[j];

                        // submit work for validation
                        Context.SubmitWork(this, work, GetType().Name);
                    }

                // clear output buffer
                if (outputDirty)
                    clQueue.WriteToBuffer(outputZero, outputAlt ? clBuffer0 : clBuffer1, true, events);

                // execute kernel with computed values
                clQueue.Finish();
                clKernel.SetValueArgument(0, round1State[0]);
                clKernel.SetValueArgument(1, round1State[1]);
                clKernel.SetValueArgument(2, round1State[2]);
                clKernel.SetValueArgument(3, round1State[3]);
                clKernel.SetValueArgument(4, round1State[4]);
                clKernel.SetValueArgument(5, round1State[5]);
                clKernel.SetValueArgument(6, round1State[6]);
                clKernel.SetValueArgument(7, round1State[7]);
                clKernel.SetValueArgument(8, round1State2Pre[4]);
                clKernel.SetValueArgument(9, round1State2Pre[5]);
                clKernel.SetValueArgument(10, round1State2Pre[6]);
                clKernel.SetValueArgument(11, round1State2Pre[0]);
                clKernel.SetValueArgument(12, round1State2Pre[1]);
                clKernel.SetValueArgument(13, round1State2Pre[2]);
                clKernel.SetValueArgument(14, nonce);
                clKernel.SetValueArgument(15, W16);
                clKernel.SetValueArgument(16, W17);
                clKernel.SetValueArgument(17, W18);
                clKernel.SetValueArgument(18, W19);
                clKernel.SetValueArgument(19, W31);
                clKernel.SetValueArgument(20, W32);
                clKernel.SetValueArgument(21, PreVal4_plus_state0);
                clKernel.SetValueArgument(22, PreVal4_plus_T1);
                clKernel.SetMemoryArgument(23, outputAlt ? clBuffer0 : clBuffer1);
                clQueue.Execute(clKernel, null, new long[] { globalWorkSize }, new long[] { localWorkSize }, events);

                // dispose of all events floating around in the list
                foreach (var e in events)
                    e.Dispose();

                // report that we just hashed the work size number of hashes
                if (!check((uint)globalWorkSize))
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
