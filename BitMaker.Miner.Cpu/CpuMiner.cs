using System;
using System.ComponentModel.Composition;
using System.Threading;
using BitMaker.Utils;

namespace BitMaker.Miner.Cpu
{

    /// <summary>
    /// CPU miner base class which provides a place to plug in the search algorithm.
    /// </summary>
    public abstract class CpuMiner : IMiner
    {

        readonly object syncRoot = new object();

        /// <summary>
        /// Context under which work is done.
        /// </summary>
        public IMinerContext Context { get; private set; }

        /// <summary>
        /// CPU we are bound to.
        /// </summary>
        public CpuDevice Cpu { get; private set; }

        /// <summary>
        /// CPU we are bound to.
        /// </summary>
        public MinerDevice Device
        {
            get { return Cpu; }
        }

        /// <summary>
        /// Signals processes to halt.
        /// </summary>
        CancellationTokenSource cts;

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
        Thread workThread;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="cpu"></param>
        protected CpuMiner(IMinerContext context, CpuDevice cpu)
        {
            Context = context;
            Cpu = cpu;
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
                    Name = GetType().Name + " : " + Cpu.Id,
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
        /// Attempts to solve the given work with the specified solver.
        /// </summary>
        /// <param name="work"></param>
        unsafe void Work(Work work)
        {
            // allocate buffers to hold hashing work
            byte[] round1Blocks, round2Blocks;
            uint[] round1State, round2State;

            // allocate buffers
            PrepareWork(work, out round1Blocks, out round1State, out round2Blocks, out round2State);

            // search for nonce value
            uint? nonce;
            fixed (byte* round1BlocksPtr = round1Blocks, round2BlocksPtr = round2Blocks)
            fixed (uint* round1StatePtr = round1State, round2StatePtr = round2State)
                nonce = Search(work, round1StatePtr, round1BlocksPtr + Sha256.SHA256_BLOCK_SIZE, round2StatePtr, round2BlocksPtr);

            // solution found!
            if (nonce != null)
            {
                // replace header data on work
                fixed (byte* headerPtr = work.Header)
                    ((uint*)headerPtr)[19] = Memory.ReverseEndian((uint)nonce);

                // let the caller know
                Context.SubmitWork(this, work, GetType().Name);
            }
        }

        /// <summary>
        /// Searches a set of broken apart work for a nonce solution, and returns it. If no solution is found, returns 
        /// <c>null</c>.
        /// </summary>
        /// <param name="work"></param>
        /// <param name="round1State"></param>
        /// <param name="round1Block2"></param>
        /// <param name="round2State"></param>
        /// <param name="round2Block1"></param>
        /// <returns></returns>
        public abstract unsafe uint? Search(Work work, uint* round1State, byte* round1Block2, uint* round2State, byte* round2Block1);

    }

}
