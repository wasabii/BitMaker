using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Net;
using System.Threading;

using Jayrock.Json;
using Jayrock.Json.Conversion;

using BitMaker.Miner.Plugin;
using BitMaker.Utils;

namespace BitMaker.Miner
{

    /// <summary>
    /// Hosts plugins and makes work available.
    /// </summary>
    public class Engine : IPluginContext
    {

        private object syncRoot = new object();

        /// <summary>
        /// Signals that we should stop.
        /// </summary>
        private CancellationTokenSource cancellation;

        /// <summary>
        /// Number of units of work that should be kept outstanding.
        /// </summary>
        private const int WORKPOOL_LIMIT = 5;

        /// <summary>
        /// Thread that keeps the work pool full.
        /// </summary>
        private Thread workPoolThread;

        /// <summary>
        /// Available work. Most recent work at the beginning.
        /// </summary>
        private LinkedList<Work> workPool;

        /// <summary>
        /// Number of milliseconds after which to expire work.
        /// </summary>
        private const int WORK_AGE_MAX = 15000;

        /// <summary>
        /// Milliseconds between recalculation of statistics.
        /// </summary>
        private const int STATISTICS_PERIOD = 1000;

        /// <summary>
        /// Timer that fires to collect statistics.
        /// </summary>
        private Timer statisticsTimer;

        /// <summary>
        /// Collects statistics about hash rate per second.
        /// </summary>
        private int hashesPerSecond;

        /// <summary>
        /// To report on hash generation rate.
        /// </summary>
        private int hashCount;

        /// <summary>
        /// Imported plugins.
        /// </summary>
        [ImportMany]
        public IEnumerable<IPlugin> Plugins { get; set; }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public Engine()
        {
            // import available plugins
            new CompositionContainer(new DirectoryCatalog(".", "*.dll")).SatisfyImportsOnce(this);
        }

        /// <summary>
        /// Starts execution of the miner. Blocks until startup is complete.
        /// </summary>
        public void Start()
        {
            lock (syncRoot)
            {
                // will let us stop various processes later
                cancellation = new CancellationTokenSource();

                // thread that ensures there is work available in the pool
                workPool = new LinkedList<Work>();
                workPoolThread = new Thread(WorkPoolThread);
                workPoolThread.Start();

                // recalculate statistics periodically
                statisticsTimer = new Timer(CalculateStatistics, null, 0, STATISTICS_PERIOD);

                // start each plugin
                foreach (var i in Plugins)
                {
                    Console.WriteLine("Starting: {0}", i.GetType().Name);
                    i.Start(this);
                }
            }
        }

        /// <summary>
        /// Stops execution of the miner. Blocks until shutdown is complete.
        /// </summary>
        public void Stop()
        {
            lock (syncRoot)
            {
                // shut down each plugin
                foreach (var i in Plugins)
                {
                    Console.WriteLine("Stopping: {0}", i.GetType().Name);
                    i.Stop();
                }

                // cancel various processes
                cancellation.Cancel();
                lock (workPool) Monitor.PulseAll(workPool);
                workPoolThread.Join();

                // stop updating statistics
                statisticsTimer.Dispose();

                // clear statistics
                hashCount = 0;
                hashesPerSecond = 0;
            }
        }

        /// <summary>
        /// Reports the average number of hashes being generated per-second.
        /// </summary>
        public int HashesPerSecond
        {
            get { return hashesPerSecond; }
        }

        /// <summary>
        /// Gets a new unit of work.
        /// </summary>
        /// <returns></returns>
        public Work GetWork()
        {
            Work work = null;

            while (work == null)
                lock (workPool)
                {
                    // obtain first item
                    while (workPool.First == null && !cancellation.IsCancellationRequested)
                        Monitor.Wait(workPool, 2000);

                    // pop node out of list
                    work = workPool.First != null ? workPool.First.Value : null;
                    if (work != null)
                        workPool.RemoveFirst();

                    Monitor.PulseAll(workPool);

                    if (cancellation.IsCancellationRequested)
                        return null;
                }

            return work;
        }

        /// <summary>
        /// Accepts a completed unit of work and returns <c>true</c> if it is accepted.
        /// </summary>
        /// <param name="work"></param>
        /// <returns></returns>
        public bool SubmitWork(Work work)
        {
            return SubmitWorkImpl(work);
        }

        /// <summary>
        /// Reports 
        /// </summary>
        /// <param name="plugin"></param>
        /// <param name="count"></param>
        public void ReportHashes(IPlugin plugin, int count)
        {
            Interlocked.Add(ref hashCount, count);
        }

        /// <summary>
        /// Ensures there is always uptodate work available in the pool.
        /// </summary>
        private void WorkPoolThread()
        {
            while (!cancellation.IsCancellationRequested)
            {
                // wait until we are signaled
                lock (workPool)
                    while (workPool.Count >= WORKPOOL_LIMIT && !cancellation.IsCancellationRequested)
                        Monitor.Wait(workPool, 2000);

                // obtain new work without locking pool, if required
                var work = workPool.Count < WORKPOOL_LIMIT ? GetWorkImpl() : null;

                lock (workPool)
                {
                    // add the new item, if we obtained it
                    if (work != null)
                    {
                        workPool.AddFirst(work);

                        // when the item we're adding is expired, we might want to rescan the pool
                        work.Token.Register(() =>
                        {
                            lock (workPool)
                                Monitor.PulseAll(workPool);
                        });
                    }

                    // scrape expired items off the bottom
                    while (workPool.Last != null && workPool.Last.Value.Token.IsCancellationRequested)
                        workPool.RemoveLast();

                    // let anybody waiting on new items know
                    Monitor.PulseAll(workPool);
                }
            }
        }

        /// <summary>
        /// Recalculates statistics.
        /// </summary>
        /// <param name="state"></param>
        private void CalculateStatistics(object state)
        {
            int hc = Interlocked.Exchange(ref hashCount, 0);

            // average the previous value with the new value
            hashesPerSecond = (hashesPerSecond + (hc / (STATISTICS_PERIOD / 1000))) / 2;
        }

        #region JSON-RPC

        /// <summary>
        /// Generates a <see cref="T:HttpWebRequest"/> for communicating with the node's JSON-API.
        /// </summary>
        /// <returns></returns>
        private static HttpWebRequest RpcOpen()
        {
            // configuration info, containing user name and password
            var url = ConfigurationSection.GetDefaultSection().Url;
            var user = url.UserInfo.Split(':');

            // create request, authenticating using information in the url
            var req = (HttpWebRequest)HttpWebRequest.Create(url);
            req.Credentials = new NetworkCredential(user[0], user[1]);
            req.PreAuthenticate = true;
            req.Method = "POST";
            return req;
        }

        /// <summary>
        /// Invokes the 'getwork' JSON method and parses the result into a new <see cref="T:Work"/> instance.
        /// </summary>
        /// <returns></returns>
        private static Work GetWorkImpl()
        {
            var req = RpcOpen();

            // submit method invocation
            using (var txt = new StreamWriter(req.GetRequestStream()))
            using (var wrt = new JsonTextWriter(txt))
            {
                wrt.WriteStartObject();
                wrt.WriteMember("method");
                wrt.WriteString("getwork");
                wrt.WriteMember("params");
                wrt.WriteStartArray();
                wrt.WriteEndArray();
                wrt.WriteEndObject();
                wrt.Flush();
            }

            // retrieve invocation response
            using (var txt = new StreamReader(req.GetResponse().GetResponseStream()))
            using (var rdr = new JsonTextReader(txt))
            {
                if (!rdr.MoveToContent() && rdr.Read())
                    throw new JsonException("Unexpected content from 'getwork'.");

                var response = JsonConvert.Import<JsonGetWork>(rdr);
                if (response == null)
                    throw new JsonException("No response returned.");

                if (response.Error != null)
                    Console.WriteLine("JSON-RPC: {0}", response.Error);

                var result = response.Result;
                if (result == null)
                    return null;

                // create a timer which cancels the work after a period
                var cts = new CancellationTokenSource();
                var tmr = new Timer(i => cts.Cancel(), null, WORK_AGE_MAX, Timeout.Infinite);
                cts.Token.Register(() => tmr.Dispose());

                // generate new work instance
                var work = new Work()
                {
                    Token = cts.Token,
                    Header = Memory.Decode(result.Data),
                    Target = Memory.Decode(result.Target),
                    Midstate = Memory.Decode(result.Midstate),
                };

                if (work.Header == null || work.Header.Length != 128)
                    throw new InvalidDataException("Received header is not valid.");

                if (work.Target == null || work.Target.Length != 32)
                    throw new InvalidDataException("Received target is not valid.");

                return work;
            }
        }

        /// <summary>
        /// Invokes the 'getwork' JSON method, submitting the proposed work. Returns <c>true</c> if the service accepts
        /// the proposed work.
        /// </summary>
        /// <param name="work"></param>
        /// <returns></returns>
        private static unsafe bool SubmitWorkImpl(Work work)
        {
            var req = RpcOpen();

            // copy passed header data to new header buffer
            var data = Sha256.AllocateInputBuffer(80);
            Array.Copy(work.Header, data, 80);

            // prepare header buffer with SHA-256
            Sha256.Prepare(data, 80, 0);
            Sha256.Prepare(data, 80, 1);

            // server expects buffer endian reversed
            fixed (byte* dataPtr = data)
                Memory.ReverseEndian((uint*)dataPtr, Sha256.SHA256_BLOCK_SIZE * 2 / 8);

            using (var txt = new StreamWriter(req.GetRequestStream()))
            using (var wrt = new JsonTextWriter(txt))
            {
                wrt.WriteStartObject();
                wrt.WriteMember("method");
                wrt.WriteString("getwork");
                wrt.WriteMember("params");
                wrt.WriteStartArray();
                wrt.WriteString(Memory.Encode(data));
                wrt.WriteEndArray();
                wrt.WriteEndObject();
                wrt.Flush();
            }

            using (var txt = new StreamReader(req.GetResponse().GetResponseStream()))
            using (var rdr = new JsonTextReader(txt))
            {
                if (!rdr.MoveToContent() && rdr.Read())
                    throw new JsonException("Unexpected content from 'getwork <data>'.");

                var response = JsonConvert.Import<JsonSubmitWork>(rdr);
                if (response == null)
                    throw new JsonException("No response returned.");

                if (response.Error != null)
                    Console.WriteLine("JSON-RPC: {0}", response.Error);

                return response.Result;
            }
        }

        #endregion

    }

}
