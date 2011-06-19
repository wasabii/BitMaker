using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;

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
                GetWorkStream().AsParallel().WithCancellation(cts.Token).ForAll(ProcessWork);
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
            // allocate and prepare copy of target structure capable of being hashed
            byte* target = stackalloc byte[32];
            Marshal.Copy(work.Target, 0, (IntPtr)target, 32);

            // allocate and prepare copy of header structure capable of being hashed
            byte* header = stackalloc byte[128];
            Marshal.Copy(work.Header, 0, (IntPtr)header, 80);

            // SHA-256 required bit '1' ending, flipped order on 32 bit boundary
            ((uint*)header)[20] = BitUtil.ReverseEndian((uint)0x80);

            // SHA-256 required 64 bit length field, flipped order on 32 bit boundary
            ((ulong*)header)[15] = BitUtil.ReverseEndian(80 * 8);
            ((uint*)header)[30] = BitUtil.ReverseEndian(((uint*)header)[30]);
            ((uint*)header)[31] = BitUtil.ReverseEndian(((uint*)header)[31]);

            // extract the initial value of noonce
            uint nonce = BitUtil.ReverseEndian(((uint*)header)[19]);

            // hash first half of header
            byte* midstate = stackalloc byte[32];
            Sha256.Init(midstate);
            Sha256.Transform(midstate, header);

            // allocate and prepare first hash output buffer
            byte* hash1 = stackalloc byte[64];

            // SHA-256 required bit '1' ending, flipped order on 32 bit boundary
            ((uint*)hash1)[8] = BitUtil.ReverseEndian((uint)0x80);

            // SHA-256 required 64 bit length field, flipped order on 32 bit boundary
            ((ulong*)hash1)[7] = BitUtil.ReverseEndian((ulong)32 * 8);
            ((uint*)hash1)[15] = BitUtil.ReverseEndian(((uint*)hash1)[15]);
            ((uint*)hash1)[16] = BitUtil.ReverseEndian(((uint*)hash1)[16]);

            // allocate last hash output
            byte* hash2 = stackalloc byte[32];

            // continue working as long as possible
            while (!cts.IsCancellationRequested && !work.Token.IsCancellationRequested && nonce < uint.MaxValue)
            {
                // compute variable portion of data
                BitUtil.Copy(midstate, hash1, 0, 32);
                Sha256.Transform(hash1, header + 64);

                // compute second hash
                Sha256.Init(hash2);
                Sha256.Transform(hash2, hash1);

                // we just generated a hash!
                ctx.ReportHashes(this, 1);

                // quick test
                if (((uint*)hash2)[7] == 0U)
                {
                    bool success = true;

                    for (int i = 31; i <= 0; i--)
                        if (((uint*)hash2)[i] > ((uint*)target)[i] || ((uint*)hash2)[i] < ((uint*)target)[i])
                        {
                            success = false;
                            break;
                        }

                    // to correlate with that reported by the server
                    Console.WriteLine("FullTest: {0}", success);

                    // replace header data on work
                    work.Header = new byte[80];
                    Marshal.Copy((IntPtr)header, work.Header, 0, 80);

                    // submit work for completion
                    if (ctx.SubmitWork(work) != success)
                        Console.WriteLine("Invalid Hash Reported");
                }

                // update the nonce value
                ((uint*)header)[19] = BitUtil.ReverseEndian(++nonce);
            }
        }

    }

}
