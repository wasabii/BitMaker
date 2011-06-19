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
        private const int WORKPOOL_LIMIT = 10;

        /// <summary>
        /// Thread that keeps the work pool full.
        /// </summary>
        private Thread workPoolThread;

        /// <summary>
        /// Available work. Most recent work at the beginning.
        /// </summary>
        private BlockingCollection<Work> workPool;

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
                workPool = new BlockingCollection<Work>(new ConcurrentStack<Work>(), WORKPOOL_LIMIT);
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
            try
            {
                return workPool.Take(cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
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
            var ct = cancellation.Token;

            // continually try to add more work to the pool
            try
            {
                while (!ct.IsCancellationRequested)
                    workPool.Add(GetWorkImpl(), ct);
            }
            catch (OperationCanceledException)
            {
                // ignore
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
            var req = (HttpWebRequest)HttpWebRequest.Create("http://api.bitcoin.cz:8332/");
            req.Credentials = new NetworkCredential("wasabi.kyoto", "10241024abcdabcd");
            //var req = (HttpWebRequest)HttpWebRequest.Create("http://tokyo.larvalstage.net:8332/");
            //req.Credentials = new NetworkCredential("wasabi", "J5qTfh11SDTrxfFbdHf0QYEXWyI2gNwT");
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
                wrt.WriteMember("id");
                wrt.WriteString(new Guid().ToString());
                wrt.WriteEndObject();
                wrt.Flush();
                txt.WriteLine();
                txt.WriteLine();
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

                var result = response.Result;
                if (result == null)
                    throw new JsonException("Response missing 'result' value.");

                // create a timer which cancels the work after a period
                var cts = new CancellationTokenSource();
                var tmr = new Timer(i => cts.Cancel(), null, WORK_AGE_MAX, Timeout.Infinite);
                cts.Token.Register(() => tmr.Dispose());

                // generate new work instance
                return new Work()
                {
                    Token = cts.Token,
                    Header = Utils.DecodeHex(result.Data, 80),
                    Target = Utils.DecodeHex(result.Target, 32),
                };
            }
        }

        /// <summary>
        /// Invokes the 'getwork' JSON method, submitting the proposed work. Returns <c>true</c> if the service accepts
        /// the proposed work.
        /// </summary>
        /// <param name="work"></param>
        /// <returns></returns>
        private static bool SubmitWorkImpl(Work work)
        {
            var req = RpcOpen();

            using (var txt = new StreamWriter(req.GetRequestStream()))
            using (var wrt = new JsonTextWriter(txt))
            {
                wrt.WriteStartObject();
                wrt.WriteMember("method");
                wrt.WriteString("getwork");
                wrt.WriteMember("params");
                wrt.WriteStartArray();
                wrt.WriteString(Utils.EncodeHex(work.Header));
                wrt.WriteEndArray();
                wrt.WriteMember("id");
                wrt.WriteString(new Guid().ToString());
                wrt.WriteEndObject();
                wrt.Flush();
                txt.WriteLine();
                txt.WriteLine();
            }

            using (var txt = new StreamReader(req.GetResponse().GetResponseStream()))
            using (var rdr = new JsonTextReader(txt))
            {
                if (rdr.MoveToContent() && rdr.Read() && rdr.ReadMember() == "result")
                    return rdr.ReadBoolean();
                else
                    throw new JsonException("Unexpected result to 'getwork <data>'.");
            }
        }

        #endregion

    }

}
