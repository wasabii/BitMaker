using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Web;

using BitMaker.Utils;

using Jayrock.Json;
using Jayrock.Json.Conversion;

namespace BitMaker.Miner
{

    /// <summary>
    /// Hosts plugins and makes work available.
    /// </summary>
    public class MinerHost : IMinerContext
    {

        private object syncRoot = new object();

        /// <summary>
        /// Thread that starts execution of the miners.
        /// </summary>
        private Thread mainThread;

        /// <summary>
        /// Indicates that the host should start.
        /// </summary>
        private bool run;

        /// <summary>
        /// Indicates that the host has started.
        /// </summary>
        private bool running;

        /// <summary>
        /// Gets the current block number
        /// </summary>
        public uint CurrentBlockNumber { get; private set; }

        /// <summary>
        /// Milliseconds between refreshes of block number.
        /// </summary>
        private static int refreshPeriod = (int)TimeSpan.FromSeconds(15).TotalMilliseconds;

        /// <summary>
        /// Thread that pulls current block.
        /// </summary>
        private Thread refreshThread;

        /// <summary>
        /// Url delivered by the server for long pulling.
        /// </summary>
        private Uri refreshLongPollUrl;

        /// <summary>
        /// Milliseconds between recalculation of statistics.
        /// </summary>
        private static int statisticsPeriod = (int)TimeSpan.FromSeconds(.25).TotalMilliseconds;

        /// <summary>
        /// Time the statistics were last calculated.
        /// </summary>
        private Stopwatch statisticsStopWatch;

        /// <summary>
        /// Timer that fires to collect statistics.
        /// </summary>
        private System.Timers.Timer statisticsTimer;

        /// <summary>
        /// Collects statistics about hash rate per second.
        /// </summary>
        private int hashesPerSecond;

        /// <summary>
        /// To report on hash generation rate.
        /// </summary>
        private int statisticsHashCount;

        /// <summary>
        /// Tracks previous hash counts and elapsed time.
        /// </summary>
        private LinkedList<Tuple<long, long>> statisticsHistory;

        /// <summary>
        /// Total hash count.
        /// </summary>
        private long hashCount;

        /// <summary>
        /// Available miner factories.
        /// </summary>
        [ImportMany]
        public IEnumerable<IMinerFactory> MinerFactories { get; set; }

        /// <summary>
        /// Currently executing miners.
        /// </summary>
        public List<MinerEntry> Miners { get; private set; }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public MinerHost()
        {
            // import available plugins
            new CompositionContainer(new DirectoryCatalog(".", "*.dll")).SatisfyImportsOnce(this);

            // default value
            Miners = new List<MinerEntry>();
            CurrentBlockNumber = 0;

            // thread handles start and stop
            mainThread = new Thread(MainThread);
            mainThread.IsBackground = true;
            mainThread.Name = "BitMaker.Miner Host Thread";
            mainThread.Start();
        }

        /// <summary>
        /// Starts execution of the miners.
        /// </summary>
        public void Start()
        {
            lock (syncRoot)
            {
                run = running = true;
                Monitor.Pulse(syncRoot);
            }
        }

        /// <summary>
        /// Stops execution of the miner. Blocks until shutdown is complete.
        /// </summary>
        public void Stop()
        {
            lock (syncRoot)
            {
                run = false;
                Monitor.Pulse(syncRoot);

                while (running)
                    Monitor.Wait(syncRoot, 5000);
            }
        }

        /// <summary>
        /// Begins program execution on an independent thread so as not to block callers.
        /// </summary>
        private void MainThread()
        {
            while (true)
            {
                lock (syncRoot)
                    while (!run)
                        Monitor.Wait(syncRoot, 15000);

                // begin timer to compile statistics, even while testing miners
                lock (syncRoot)
                {
                    // clear statistics
                    hashesPerSecond = 0;
                    statisticsHashCount = 0;
                    statisticsHistory = new LinkedList<Tuple<long, long>>();
                    hashCount = 0;

                    statisticsTimer = new System.Timers.Timer();
                    statisticsTimer.Elapsed += (s, a) => CalculateStatistics();
                    statisticsTimer.AutoReset = false;
                    statisticsTimer.Interval = statisticsPeriod;
                    statisticsTimer.Start();
                    statisticsStopWatch = new Stopwatch();
                }

                // resources, with the miner factories that can use them
                var resourceSets = MinerFactories
                    .SelectMany(i => i.Resources.Select(j => new { Factory = i, Resource = j }))
                    .GroupBy(i => i.Resource)
                    .Select(i => new { Resource = i.Key, Factories = i.Select(j => j.Factory).ToList() })
                    .ToList();

                // for each resource, start the appropriate miner
                foreach (var resourceSet in resourceSets)
                {
                    IMinerFactory topFactory = null;
                    int topHashCount = 0;

                    if (resourceSet.Factories.Count == 1)
                        // only a single miner factory can consume this resource, so just use it
                        topFactory = resourceSet.Factories[0];
                    else
                    {
                        // multiple miners claim the ability to consume this resource
                        foreach (var factory in resourceSet.Factories)
                        {
                            // sample the hash rate from the proposed miner
                            int hashes = SampleMiner(factory, resourceSet.Resource);
                            if (hashes > topHashCount)
                            {
                                topFactory = factory;
                                topHashCount = hashes;
                            }

                            // break out prematurely if told to stop
                            if (!run)
                                break;
                        }
                    }

                    // break out prematurely if told to stop
                    if (!run)
                        break;

                    // start production miner
                    StartMiner(topFactory, resourceSet.Resource, this);
                }

                // periodically check latest block number
                refreshThread = new Thread(RefreshThreadMain);
                refreshThread.Start();

                // wait until we're told to terminate
                lock (syncRoot)
                    while (run)
                        Monitor.Wait(syncRoot, 5000);

                // shut down each executing miner
                while (Miners.Any())
                    StopMiner(Miners[0].Miner);

                // stop timers
                lock (syncRoot)
                {
                    statisticsTimer.Dispose();
                    statisticsTimer = null;
                    statisticsStopWatch = null;
                    statisticsHistory = null;

                    if (refreshThread != null)
                    {
                        refreshThread.Join();
                        refreshThread = null;
                    }

                    // indicate that we have stopped
                    running = false;
                    Monitor.PulseAll(syncRoot);
                }
            }
        }

        /// <summary>
        /// Starts a new miner.
        /// </summary>
        /// <param name="factory"></param>
        /// <param name="resource"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        private IMiner StartMiner(IMinerFactory factory, MinerResource resource, IMinerContext context)
        {
            lock (syncRoot)
            {
                var miner = factory.StartMiner(context, resource);
                Miners.Add(new MinerEntry(miner, resource));
                return miner;
            }
        }

        /// <summary>
        /// Stops the given miner.
        /// </summary>
        /// <param name="miner"></param>
        private void StopMiner(IMiner miner)
        {
            lock (syncRoot)
            {
                var entry = Miners.Single(i => i.Miner == miner);
                miner.Stop();
                Miners.Remove(entry);
            }
        }

        /// <summary>
        /// Total number of hashes generated by miner.
        /// </summary>
        public long HashCount
        {
            get { return hashCount; }
        }

        /// <summary>
        /// Reports the average number of hashes being generated per-second.
        /// </summary>
        public int HashesPerSecond
        {
            get { return hashesPerSecond; }
        }

        /// <summary>
        /// Invokes the given function repeatidly, until it succeeds, with a delay.
        /// </summary>
        /// <param name="func"></param>
        /// <param name="delay"></param>
        /// <returns></returns>
        private static R Retry<R>(Func<R> func, TimeSpan delay)
        {
            while (true)
                try
                {
                    return func();
                }
                catch (Exception)
                {
                    Thread.Sleep(delay);
                }
        }

        /// <summary>
        /// Gets a new unit of work.
        /// </summary>
        /// <returns></returns>
        public Work GetWork(IMiner miner, string comment)
        {
            return Retry(() =>
            {
                return GetWorkRpc(miner, comment);
            }, TimeSpan.FromSeconds(5));
        }

        /// <summary>
        /// Accepts a completed unit of work and returns <c>true</c> if it is accepted.
        /// </summary>
        /// <param name="work"></param>
        /// <returns></returns>
        public bool SubmitWork(IMiner miner, Work work, string comment)
        {
            // validate header
            if (!work.Validate())
                Console.WriteLine("INVALID : {0,10} {1}", miner.GetType().Name, Memory.Encode(work.Header));

            return Retry(() => SubmitWorkRpc(miner, work, comment), TimeSpan.FromSeconds(5));
        }

        /// <summary>
        /// Reports 
        /// </summary>
        /// <param name="plugin"></param>
        /// <param name="count"></param>
        public void ReportHashes(IMiner plugin, uint count)
        {
            Interlocked.Add(ref statisticsHashCount, (int)count);
        }

        /// <summary>
        /// Calculates statistics since previous invocation.
        /// </summary>
        private void CalculateStatistics()
        {
            lock (syncRoot)
            {
                // obtain current values, then restart watch
                statisticsStopWatch.Stop();
                var hc = Interlocked.Exchange(ref statisticsHashCount, 0);
                var df = statisticsStopWatch.ElapsedMilliseconds;
                statisticsStopWatch.Restart();

                // append current statistics to the history
                statisticsHistory.AddLast(new Tuple<long, long>(df, hc));

                // keep statistics history limited
                if (statisticsHistory.Count > 25)
                    statisticsHistory.RemoveFirst();

                // add periodic count to total
                Interlocked.Add(ref hashCount, hc);

                // total ellapsed time and hashes
                long hdf = statisticsHistory.Sum(i => i.Item1);
                long hhc = statisticsHistory.Sum(i => i.Item2);

                if (hdf > 1000)
                    hashesPerSecond = (int)(hhc / (hdf / 1000));

                // restart timer
                statisticsTimer.Start();
            }
        }

        /// <summary>
        /// Entry point for thread responsible for detecting changes in the current block number.
        /// </summary>
        private void RefreshThreadMain()
        {
            while (run)
            {
                try
                {
                    if (refreshLongPollUrl == null)
                    {
                        GetWorkRpc(null, null);
                        Thread.Sleep(refreshPeriod);
                    }
                    else
                        GetWorkLp(null, null);
                }
                catch (WebException)
                {
                    // ignore
                }
            }
        }

        #region JSON-RPC

        private static Uri GetRpcUri()
        {
            return ConfigurationSection.GetDefaultSection().Url;
        }

        private static HttpWebRequest Open(Uri url, string method, IMiner miner, string comment)
        {
            // extract user information from url
            var user = url.UserInfo.Split(':').Select(i => HttpUtility.UrlDecode(i)).ToArray();

            // create request, authenticating using information in the url
            var req = (HttpWebRequest)HttpWebRequest.Create(url);
            req.Timeout = (int)TimeSpan.FromSeconds(60).TotalMilliseconds;
            req.Credentials = new NetworkCredential(user[0], user[1]);
            req.PreAuthenticate = true;
            req.Method = method;
            req.Pipelined = true;
            req.UserAgent = "BitMaker";
            req.Headers["X-BitMaker-MachineName"] = Environment.MachineName;

            if (miner != null)
                req.Headers["X-BitMaker-Miner"] = miner.GetType().Name;

            if (!string.IsNullOrWhiteSpace(comment))
                req.Headers["X-BitMaker-Comment"] = comment;

            return req;
        }

        /// <summary>
        /// Creates a <see cref="HttpWebRequest"/> for JSON-RPC.
        /// </summary>
        /// <param name="miner"></param>
        /// <param name="comment"></param>
        /// <returns></returns>
        private static HttpWebRequest OpenRpc(IMiner miner, string comment)
        {
            return Open(GetRpcUri(), "POST", miner, comment);
        }

        /// <summary>
        /// Creates a <see cref="HttpWebRequest"/> for long-polling.
        /// </summary>
        /// <param name="miner"></param>
        /// <param name="comment"></param>
        /// <returns></returns>
        private HttpWebRequest OpenLp(IMiner miner, string comment)
        {
            return Open(refreshLongPollUrl, "GET", miner, comment);
        }

        /// <summary>
        /// Parses a <see cref="WebResponse"/> containing the results of a 'getwork' request.
        /// </summary>
        /// <param name="webResponse"></param>
        /// <returns></returns>
        private Work ParseGetWork(WebResponse webResponse)
        {
            // obtain and update current block number
            uint blockNumber = 0;
            if (webResponse.Headers["X-Blocknum"] != null)
                CurrentBlockNumber = blockNumber = uint.Parse(webResponse.Headers["X-Blocknum"]);

            // parse and update long poll url value if present
            var longPollUrlStr = webResponse.Headers["X-Long-Polling"];
            if (longPollUrlStr == null)
                refreshLongPollUrl = null;
            else if (longPollUrlStr == "")
                refreshLongPollUrl = GetRpcUri();
            else if (longPollUrlStr.StartsWith("http:") || longPollUrlStr.StartsWith("https:"))
                refreshLongPollUrl = new Uri(longPollUrlStr);
            else
                refreshLongPollUrl = new Uri(GetRpcUri(), longPollUrlStr);

            // retrieve invocation response
            using (var txt = new StreamReader(webResponse.GetResponseStream()))
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

                // decode data
                var data = Memory.Decode(result.Data);
                if (data.Length != 128)
                    throw new InvalidDataException("Received data is not valid.");

                // extract only header portion
                var header = new byte[80];
                Array.Copy(data, header, 80);

                // decode target
                var target = Memory.Decode(result.Target);
                if (target.Length != 32)
                    throw new InvalidDataException("Received target is not valid.");

                // generate new work instance
                var work = new Work()
                {
                    BlockNumber = blockNumber,
                    Header = header,
                    Target = target,
                };

                // release connection
                webResponse.Close();

                return work;
            }
        }

        /// <summary>
        /// Invokes the 'getwork' JSON method and parses the result into a new <see cref="T:Work"/> instance.
        /// </summary>
        /// <returns></returns>
        private Work GetWorkRpc(IMiner miner, string comment)
        {
            var req = OpenRpc(miner, comment);

            // submit method invocation
            using (var txt = new StreamWriter(req.GetRequestStream()))
            using (var wrt = new JsonTextWriter(txt))
            {
                wrt.WriteStartObject();
                wrt.WriteMember("id");
                wrt.WriteString("json");
                wrt.WriteMember("method");
                wrt.WriteString("getwork");
                wrt.WriteMember("params");
                wrt.WriteStartArray();
                wrt.WriteEndArray();
                wrt.WriteEndObject();
                wrt.Flush();
            }

            return ParseGetWork(req.GetResponse());
        }

        /// <summary>
        /// Initiates a long-poll request for work.
        /// </summary>
        /// <param name="miner"></param>
        /// <param name="comment"></param>
        /// <returns></returns>
        private Work GetWorkLp(IMiner miner, string comment)
        {
            var req = OpenLp(miner, comment);
            var asy = req.BeginGetResponse(null, null);

            // wait until we're told to shutdown, or we run out of time
            var elapsed = 0;
            while (run && elapsed < TimeSpan.FromSeconds(60).TotalMilliseconds)
            {
                asy.AsyncWaitHandle.WaitOne(1000);
                elapsed += 1000;
            }

            if (!asy.IsCompleted)
            {
                // if it never completed, abort it
                req.Abort();
                return null;
            }
            else
                // otherwise parse the result
                return ParseGetWork(req.EndGetResponse(asy));
        }

        /// <summary>
        /// Invokes the 'getwork' JSON method, submitting the proposed work. Returns <c>true</c> if the service accepts
        /// the proposed work.
        /// </summary>
        /// <param name="work"></param>
        /// <returns></returns>
        private unsafe bool SubmitWorkRpc(IMiner miner, Work work, string comment)
        {
            var req = OpenRpc(miner, comment);

            // header needs to have SHA-256 padding appended
            var data = Sha256.AllocateInputBuffer(80);

            // prepare header buffer with SHA-256
            Sha256.Prepare(data, 80, 0);
            Sha256.Prepare(data, 80, 1);

            // dump header data on top of padding
            Array.Copy(work.Header, data, 80);

            // encode in proper format
            var solution = Memory.Encode(data);

            Console.WriteLine();
            Console.WriteLine("SOLUTION: {0,10} {1}", miner.GetType().Name, Memory.Encode(work.Header));
            Console.WriteLine();
            Console.WriteLine();

            using (var txt = new StreamWriter(req.GetRequestStream()))
            using (var wrt = new JsonTextWriter(txt))
            {
                wrt.WriteStartObject();
                wrt.WriteMember("id");
                wrt.WriteString("json");
                wrt.WriteMember("method");
                wrt.WriteString("getwork");
                wrt.WriteMember("params");
                wrt.WriteStartArray();
                wrt.WriteString(solution);
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

                Console.WriteLine();
                Console.WriteLine("{0}: {1,10} {2}", response.Result ? "ACCEPTED" : "REJECTED", miner.GetType().Name, Memory.Encode(work.Header));
                Console.WriteLine();
                Console.WriteLine();

                return response.Result;
            }
        }

        #endregion

        /// <summary>
        /// Context to be used to sample the hash rate of a miner.
        /// </summary>
        private class SampleMinerContext : IMinerContext
        {

            private MinerHost host;

            internal int hashCount;

            /// <summary>
            /// Initializes a new instance.
            /// </summary>
            /// <param name="host"></param>
            public SampleMinerContext(MinerHost host)
            {
                this.host = host;
            }

            public Work GetWork(IMiner miner, string comment)
            {
                return host.GetWork(miner, comment);
            }

            public bool SubmitWork(IMiner miner, Work work, string comment)
            {
                return host.SubmitWork(miner, work, comment);
            }

            public void ReportHashes(IMiner plugin, uint count)
            {
                // keep our own counter of hashes
                Interlocked.Add(ref hashCount, (int)count);

                // report to main host, these afterall do count for something
                host.ReportHashes(plugin, count);
            }

            public uint CurrentBlockNumber
            {
                get { return host.CurrentBlockNumber; }
            }

        }

        /// <summary>
        /// Starts a miner for a predetermined amount of time and records it's hash count.
        /// </summary>
        private int SampleMiner(IMinerFactory factory, MinerResource resource)
        {
            // context for sample run of miner
            var ctx = new SampleMinerContext(this);

            // begin the miner
            var miner = StartMiner(factory, resource, ctx);

            // allow the miner to work for a few seconds
            Thread.Sleep(5000);

            // pull the hash count immediately before stopping
            var hashCount = ctx.hashCount;
            StopMiner(miner);

            // return the hash count generated by the miner in the alloted time
            return hashCount;
        }

    }

}
