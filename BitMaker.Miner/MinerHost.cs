using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
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

        static readonly TimeSpan STATISTICS_UPDATE = TimeSpan.FromSeconds(1);
        static readonly TimeSpan STATISTICS_DELAY = TimeSpan.FromSeconds(6);

        /// <summary>
        /// Gets the current time in nano-seconds.
        /// </summary>
        /// <returns></returns>
        static long Now()
        {
            return DateTime.UtcNow.Ticks * 100 / 1000000;
        }

        /// <summary>
        /// Contains MEF integrated components.
        /// </summary>
        CompositionContainer container = new CompositionContainer(new ApplicationCatalog());

        object syncRoot = new object();

        /// <summary>
        /// Thread that starts execution of the miners.
        /// </summary>
        Thread mainThread;

        /// <summary>
        /// Indicates that the host should start.
        /// </summary>
        bool run;

        /// <summary>
        /// Indicates that the host has started.
        /// </summary>
        bool running;

        /// <summary>
        /// Available pools to retrieve work from.
        /// </summary>
        List<Pool> pools;

        /// <summary>
        /// Timer that fires to collect statistics.
        /// </summary>
        System.Timers.Timer statisticsTimer;

        long startTime;
        long hashCount;
        long previousHashCount;
        double previousAdjustedHashCount;
        long previousAdjustedStartTime;
        double hashesPerSecond;

        /// <summary>
        /// Available miner factories.
        /// </summary>
        [ImportMany]
        public IEnumerable<IMinerFactory> MinerFactories { get; set; }

        /// <summary>
        /// Currently executing miners.
        /// </summary>
        public List<IMiner> Miners { get; private set; }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public MinerHost()
        {
            // import available plugins
            container.ComposeExportedValue<IMinerContext>(this);
            container.SatisfyImportsOnce(this);

            // default value
            Miners = new List<IMiner>();

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
        void MainThread()
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
                    previousHashCount = 0;
                    previousAdjustedHashCount = 0.0;
                    previousAdjustedStartTime = startTime = Now() - 1;
                    hashesPerSecond = 0;

                    statisticsTimer = new System.Timers.Timer();
                    statisticsTimer.Elapsed += (s, a) => CalculateStatistics();
                    statisticsTimer.AutoReset = true;
                    statisticsTimer.Interval = STATISTICS_UPDATE.TotalMilliseconds;
                    statisticsTimer.Start();

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
                var resourceMiners = factories
                    .SelectMany(i => i.Miners
                        .Select(j => new
                        {
                            Factory = i,
                            Miner = j,
                            Resource = j.Device,
                        }))
                    .GroupBy(i => i.Resource)
                    .Select(i => new
                    {
                        Resource = i.Key,
                        Miners = i
                            .Select(j => j.Miner)
                            .ToList(),
                    })
                    .ToList();

                // for each resource, start the appropriate miner
                foreach (var resourceMiner in resourceMiners)
                {
                    // select miner with the top number of sample hashes, or first
                    var miner = resourceMiner.Miners.Count == 1 ? resourceMiner.Miners[0] : resourceMiner.Miners
                        .Select(i => new { Miner = i, Hashes = run ? SampleMiner(i) : 0 })
                        .OrderByDescending(i => i.Hashes)
                        .Select(i => i.Miner)
                        .FirstOrDefault();

                    // break out prematurely if told to stop
                    if (!run)
                        break;

                    // start production miner
                    StartMiner(miner);
                }

                // wait until we're told to terminate
                lock (syncRoot)
                    while (run)
                        Monitor.Wait(syncRoot, 5000);

                // shut down each executing miner
                while (Miners.Any())
                    StopMiner(Miners[0]);

                lock (syncRoot)
                {
                    // stop statistics stuff
                    statisticsTimer.Dispose();
                    statisticsTimer = null;

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
        /// <param name="miner"></param>
        /// <returns></returns>
        void StartMiner(IMiner miner)
        {
            Miners.Add(miner);
            miner.Start();
        }

        /// <summary>
        /// Stops the given miner.
        /// </summary>
        /// <param name="miner"></param>
        void StopMiner(IMiner miner)
        {
            miner.Stop();
            Miners.Remove(miner);
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
        public double HashesPerSecond
        {
            get { return hashesPerSecond; }
        }

        /// <summary>
        /// Invokes the given function repeatidly, until it succeeds, with a delay.
        /// </summary>
        /// <param name="func"></param>
        /// <param name="delay"></param>
        /// <returns></returns>
        static R Retry<R>(Func<R> func, TimeSpan delay)
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
        public void ReportHashes(IMiner plugin, long count)
        {
            Interlocked.Add(ref hashCount, count);
        }

        /// <summary>
        /// Calculates statistics since previous invocation.
        /// </summary>
        void CalculateStatistics()
        {
            statisticsTimer.Stop();

            var now = Now();
            var currentHashCount = Interlocked.Read(ref hashCount);
            var adjustedHashCount = (double)(currentHashCount - previousHashCount) / (double)(now - previousAdjustedStartTime);
            var hashLongCount = (double)currentHashCount / (double)(now - startTime) / 1000.0;

            if (now - startTime > STATISTICS_DELAY.TotalSeconds * 2)
                hashesPerSecond = (long)(adjustedHashCount + previousAdjustedHashCount) / 2.0 * 1000;

            if (now - STATISTICS_DELAY.TotalSeconds * 2 > previousAdjustedStartTime)
            {
                previousHashCount = currentHashCount;
                previousAdjustedHashCount = adjustedHashCount;
                previousAdjustedStartTime = now - 1;
            }

            statisticsTimer.Start();
        }

        /// <summary>
        /// Context to be used to sample the hash rate of a miner.
        /// </summary>
        class SampleMinerContext : IMinerContext
        {

            MinerHost host;
            internal long hashCount;

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

            public void ReportHashes(IMiner plugin, long count)
            {
                // keep our own counter of hashes
                Interlocked.Add(ref hashCount, count);

                // report to main host, these afterall do count for something
                host.ReportHashes(plugin, count);
            }

        }

        /// <summary>
        /// Starts a miner for a predetermined amount of time and records it's hash count.
        /// </summary>
        long SampleMiner(IMiner miner)
        {
            // context for sample run of miner
            var ctx = new SampleMinerContext(this);

            // begin the miner
            miner.Start();

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
