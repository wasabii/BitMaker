using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.Linq;
using System.Threading;

using BitMaker.Utils;

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
        /// Available pools to retrieve work from.
        /// </summary>
        private List<Pool> pools;

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
            new CompositionContainer(new ApplicationCatalog()).SatisfyImportsOnce(this);

            // default value
            Miners = new List<MinerEntry>();

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
            var cfg = ConfigurationSection.GetDefaultSection();

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

                    // create pools from configuration
                    pools = cfg.Pools
                        .Cast<PoolConfigurationElement>()
                        .Select(i => new Pool(i.Url))
                        .ToList();
                }

                // configured factories
                var factories = MinerFactories;
                if (cfg.Miners.Count > 0)
                    factories = factories
                        .Where(i => cfg.Miners.Any(j => j.Type.IsInstanceOfType(i)));

                // resources, with the miner factories that can use them
                var resourceSets = factories
                    .SelectMany(i => i.Resources
                        .Select(j => new 
                        {
                            Factory = i,
                            Resource = j,
                        }))
                    .GroupBy(i => i.Resource)
                    .Select(i => new
                    {
                        Resource = i.Key,
                        Factories = i
                            .Select(j => j.Factory)
                            .ToList(),
                    })
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

                // wait until we're told to terminate
                lock (syncRoot)
                    while (run)
                        Monitor.Wait(syncRoot, 5000);

                // shut down each executing miner
                while (Miners.Any())
                    StopMiner(Miners[0].Miner);

                lock (syncRoot)
                {
                    // stop statistics stuff
                    statisticsTimer.Dispose();
                    statisticsTimer = null;
                    statisticsStopWatch = null;
                    statisticsHistory = null;

                    // dispose of the pools
                    if (pools != null)
                    {
                        foreach (var pool in pools)
                            pool.Dispose();
                        pools = null;
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
            // continue attempting to get work until we are told to shut down
            while (run)
            {
                // attempt each available pool
                foreach (var pool in pools)
                {
                    // bail out of inner loop
                    if (!run)
                        break;

                    try
                    {
                        // ask the pool for work, continue if it can't deliver
                        Work work = pool.GetWorkRpc(miner, comment);
                        if (work != null)
                            return work;
                    }
                    catch (Exception)
                    {

                    }
                }

                // wait for 5 seconds before trying again
                Thread.Sleep(5000);
            }

            return null;
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

            return Retry(() => work.Pool.SubmitWorkRpc(miner, work, comment), TimeSpan.FromSeconds(5));
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
