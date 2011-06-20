using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using BitMaker.Utils;

namespace BitMaker.Miner.Plugin.Cpu
{

    [Plugin]
    public class CpuPlugin : IPlugin
    {

        private IPluginContext ctx;
        private CancellationTokenSource cts;

        public void Start(IPluginContext ctx)
        {
            this.ctx = ctx;
            this.cts = new CancellationTokenSource();

            // initialize parallel work processor
            new Thread(ProcessMain).Start();
        }

        public void Stop()
        {
            // signal thread to stop
            cts.Cancel();
        }

        /// <summary>
        /// Provides a blocking enumerable of incoming work items.
        /// </summary>
        /// <returns></returns>
        private IEnumerable<Work> GetWorkStream()
        {
            Work work;
            while ((work = ctx.GetWork()) != null)
                yield return work;
        }

        /// <summary>
        /// Entry-point for main thread. Uses PLinq to schedule all available work.
        /// </summary>
        private void ProcessMain()
        {
            try
            {
                GetWorkStream().AsParallel()
                    .WithCancellation(cts.Token)
                    //.WithDegreeOfParallelism(1)
                    .ForAll(ProcessWork);
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
        private unsafe void ProcessWork(Work work)
        {
            // bail out on stale work
            if (cts.IsCancellationRequested || work.Token.IsCancellationRequested)
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

                // pin arrays for the duration of this tight loop
                while (nonce < uint.MaxValue)
                {
                    // transform variable second half of block using saved state
                    Memory.Copy(midstatePtr, statePtr, Sha256.SHA256_STATE_SIZE);
                    Sha256.Transform(statePtr, dataPtr + Sha256.SHA256_BLOCK_SIZE);
                    // Sha256.Finalize(statePtr, hashPtr); // no need to finalize, rehashing same value

                    // compute second hash
                    // Sha256.Prepare(hashPtr, Sha256.SHA256_HASH_SIZE, 0); // no need to prepare, already done
                    Sha256.Initialize(state2Ptr);
                    Sha256.Transform(state2Ptr, (byte*)statePtr);

                    // only report and check for exit conditions every so often
                    if (nonce % 16384 == 0 && nonce > 0)
                    {
                        ctx.ReportHashes(this, 16384);
                        if (cts.IsCancellationRequested || work.Token.IsCancellationRequested)
                            break;
                    }

                    // the hash is byte order flipped state, quick check that state passes a test before doing work
                    if (state2Ptr[7] == 0U)
                    {
                        // finalize the hash, essentially reverting byte order
                        Sha256.Finalize(state2Ptr, hashPtr);

                        // replace header data on work
                        work.Header = new byte[80];
                        fixed (byte* dstHeaderPtr = work.Header)
                            Memory.Copy((uint*)dataPtr, (uint*)dstHeaderPtr, 20);

                        // submit work for completion
                        if (!ctx.SubmitWork(work))
                            Console.WriteLine("Invalid hash submitted");
                    }

                    // update the nonce value
                    ((uint*)dataPtr)[19] = Memory.ReverseEndian(++nonce);
                }
            }
        }

    }

}
