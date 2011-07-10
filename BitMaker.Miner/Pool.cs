using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Web;

using Jayrock.Json;
using Jayrock.Json.Conversion;

using BitMaker.Utils;

namespace BitMaker.Miner
{

    /// <summary>
    /// Encapsulates access to a mining pool.
    /// </summary>
    public sealed class Pool : IDisposable
    {

        /// <summary>
        /// Keeps the refresh thread alive.
        /// </summary>
        private bool run = true;

        /// <summary>
        /// Base url of the pool.
        /// </summary>
        private Uri url;

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
        private Uri longPollUrl;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="url"></param>
        internal Pool(Uri url)
        {
            this.url = url;

            CurrentBlockNumber = 0;

            // periodically check latest block number
            refreshThread = new Thread(RefreshThreadMain);
            refreshThread.Start();
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
                    if (longPollUrl == null)
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

        /// <summary>
        /// Opens a web request.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="method"></param>
        /// <param name="miner"></param>
        /// <param name="comment"></param>
        /// <returns></returns>
        private HttpWebRequest Open(Uri url, string method, IMiner miner, string comment)
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
        private HttpWebRequest OpenRpc(IMiner miner, string comment)
        {
            return Open(url, "POST", miner, comment);
        }

        /// <summary>
        /// Creates a <see cref="HttpWebRequest"/> for long-polling.
        /// </summary>
        /// <param name="miner"></param>
        /// <param name="comment"></param>
        /// <returns></returns>
        private HttpWebRequest OpenLp(IMiner miner, string comment)
        {
            return Open(longPollUrl, "GET", miner, comment);
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
                longPollUrl = null;
            else if (longPollUrlStr == "")
                longPollUrl = url;
            else if (longPollUrlStr.StartsWith("http:") || longPollUrlStr.StartsWith("https:"))
                longPollUrl = new Uri(longPollUrlStr);
            else
                longPollUrl = new Uri(url, longPollUrlStr);

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
                    Pool = this,
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
        public Work GetWorkRpc(IMiner miner, string comment)
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
        public bool SubmitWorkRpc(IMiner miner, Work work, string comment)
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
        
        /// <summary>
        /// Disposes of the pool and releases all resources associated with it.
        /// </summary>
        public void Dispose()
        {
            run = false;

            if (refreshThread != null)
            {
                refreshThread.Join();
                refreshThread = null;
            }
        }

    }

}
