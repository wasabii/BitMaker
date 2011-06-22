using System;
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
        /// Ensures we don't start or stop multiple times.
        /// </summary>
        private bool started = false;

        /// <summary>
        /// Number of milliseconds after which to expire work.
        /// </summary>
        private static int workAgeMax = (int)TimeSpan.FromMinutes(2).TotalMilliseconds;

        /// <summary>
        /// Gets the current block number
        /// </summary>
        public int CurrentBlockNumber { get; private set; }

        /// <summary>
        /// Milliseconds between recalculation of statistics.
        /// </summary>
        private static int statisticsPeriod = (int)TimeSpan.FromSeconds(3).TotalMilliseconds;

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

            // default value
            CurrentBlockNumber = -1;
        }

        /// <summary>
        /// Starts execution of the miner. Blocks until startup is complete.
        /// </summary>
        public void Start()
        {
            lock (syncRoot)
            {
                if (started)
                    return;

                // recalculate statistics periodically
                statisticsTimer = new Timer(CalculateStatistics, null, 0, statisticsPeriod);

                // start each plugin
                foreach (var i in Plugins)
                {
                    Console.WriteLine("Starting: {0}", i.GetType().Name);
                    i.Start(this);
                }

                started = true;
            }
        }

        /// <summary>
        /// Stops execution of the miner. Blocks until shutdown is complete.
        /// </summary>
        public void Stop()
        {
            lock (syncRoot)
            {
                if (!started)
                    return;

                // shut down each plugin
                foreach (var i in Plugins)
                {
                    Console.WriteLine("Stopping: {0}", i.GetType().Name);
                    i.Stop();
                }

                statisticsTimer.Dispose();
                statisticsTimer = null;

                // clear statistics
                hashCount = 0;
                hashesPerSecond = 0;
                started = false;
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
            return Retry(GetWorkImpl, TimeSpan.FromSeconds(5));
        }

        /// <summary>
        /// Invokes the given function repeatidly, until it succeeds, with a delay.
        /// </summary>
        /// <typeparam name="R"></typeparam>
        /// <param name="func"></param>
        /// <param name="delay"></param>
        /// <returns></returns>
        private R Retry<R>(Func<R> func, TimeSpan delay)
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
        /// Invokes the given function repeatidly, until it succeeds, with a delay.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="func"></param>
        /// <param name="delay"></param>
        /// <returns></returns>
        private R Retry<T, R>(Func<T, R> func, T arg0, TimeSpan delay)
        {
            while (true)
                try
                {
                    return func(arg0);
                }
                catch (Exception)
                {
                    Thread.Sleep(delay);
                }
        }

        /// <summary>
        /// Accepts a completed unit of work and returns <c>true</c> if it is accepted.
        /// </summary>
        /// <param name="work"></param>
        /// <returns></returns>
        public bool SubmitWork(Work work)
        {
            return Retry(SubmitWorkImpl, work, TimeSpan.FromSeconds(5));
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
        /// Recalculates statistics.
        /// </summary>
        /// <param name="state"></param>
        private void CalculateStatistics(object state)
        {
            int hc = Interlocked.Exchange(ref hashCount, 0);

            // average the previous value with the new value
            hashesPerSecond = (hashesPerSecond + (hc / (statisticsPeriod / 1000))) / 2;
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
            req.Pipelined = true;
            req.UserAgent = "BitMaker";
            req.Headers["X-MachineName"] = Environment.MachineName;
            return req;
        }

        /// <summary>
        /// Invokes the 'getwork' JSON method and parses the result into a new <see cref="T:Work"/> instance.
        /// </summary>
        /// <returns></returns>
        private unsafe Work GetWorkImpl()
        {
            var req = RpcOpen();

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

            var httpResponse = req.GetResponse();

            // update current block number
            if (httpResponse.Headers["X-Blocknum"] != null)
                CurrentBlockNumber = int.Parse(httpResponse.Headers["X-Blocknum"]);

            // retrieve invocation response
            using (var txt = new StreamReader(httpResponse.GetResponseStream()))
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
                var tmr = new Timer(i => cts.Cancel(), null, workAgeMax, Timeout.Infinite);
                cts.Token.Register(() => tmr.Dispose());

                // generate new work instance
                var work = new Work()
                {
                    CancellationToken = cts.Token,
                    Header = Memory.Decode(result.Data),
                    Target = Memory.Decode(result.Target),
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

            // header needs to have SHA-256 padding appended
            var data = Sha256.AllocateInputBuffer(80);

            // prepare header buffer with SHA-256
            Sha256.Prepare(data, 80, 0);
            Sha256.Prepare(data, 80, 1);

            // dump header data on top of padding
            Array.Copy(work.Header, data, 80);

            // encode in proper format
            var solution = Memory.Encode(data);

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

                return response.Result;
            }
        }

        #endregion

    }

}
