using System;
using System.Collections.Generic;
using System.Linq;

namespace BitMaker.Miner.Cpu
{

    /// <summary>
    /// CPU miner factory base class which advertises a <see cref="CpuMiner"/> as capable of consuming the system's
    /// processors. Implement this class and extend StartMiner to provide a customer miner implementation.
    /// </summary>
    public abstract class CpuMinerFactory : IMinerFactory
    {

        /// <summary>
        /// All available CPUs in the system.
        /// </summary>
        public static readonly IEnumerable<MinerResource> cpuResources =
            Enumerable.Range(0, Environment.ProcessorCount)
            .Select(i => new CpuResource() { Id = "CPU" + i })
            .ToArray();

        /// <summary>
        /// <see cref="T:CpuMiner"/> can consume all of the available processors in the system.
        /// </summary>
        public IEnumerable<MinerResource> Resources
        {
            get { return cpuResources; }
        }

        /// <summary>
        /// Invoked by the host to begin a new miner.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="resource"></param>
        /// <returns></returns>
        public IMiner StartMiner(IMinerContext context, MinerResource resource)
        {
            return StartMiner(context, (CpuResource)resource);
        }

        /// <summary>
        /// Implemented by children of <see cref="T:CpuMinerFactory"/> to start a custom miner implementation.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="resource"></param>
        /// <returns></returns>
        public abstract new IMiner StartMiner(IMinerContext context, CpuResource resource);

    }

}
