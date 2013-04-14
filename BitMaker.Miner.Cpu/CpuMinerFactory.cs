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
        static readonly IEnumerable<CpuDevice> cpus =
            Enumerable.Range(0, Environment.ProcessorCount)
            .Select(i => new CpuDevice() { Id = "CPU" + i })
            .ToArray();

        /// <summary>
        /// <see cref="T:CpuMiner"/> can consume all of the available processors in the system.
        /// </summary>
        public virtual IEnumerable<CpuDevice> Cpus
        {
            get { return cpus; }
        }

        /// <summary>
        /// <see cref="T:CpuMiner"/> can consume all of the available processors in the system.
        /// </summary>
        public virtual IEnumerable<MinerDevice> Devices
        {
            get { return cpus; }
        }

        /// <summary>
        /// Override to implement CPU miners.
        /// </summary>
        public abstract IEnumerable<IMiner> Miners { get; }

    }

}
