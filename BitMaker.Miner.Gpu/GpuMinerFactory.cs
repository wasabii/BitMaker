using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace BitMaker.Miner.Gpu
{

    /// <summary>
    /// CPU miner factory base class which advertises a <see cref="GpuMiner"/> as capable of consuming the system's
    /// processors. Implement this class and extend StartMiner to provide a customer miner implementation.
    /// </summary>
    [MinerFactory]
    public sealed class GpuMinerFactory : IMinerFactory
    {

        static readonly string[] excludedVendors = new string[]
        {
            "GenuineIntel",
        };

        /// <summary>
        /// All available GPUs in the system.
        /// </summary>
        static readonly IEnumerable<GpuDevice> gpus;

        /// <summary>
        /// Initializes the static instance.
        /// </summary>
        static GpuMinerFactory()
        {
            try
            {
                gpus = global::Cloo.ComputePlatform.Platforms
                    .SelectMany(i => i.Devices)
                    .Where(i => !excludedVendors.Contains(i.Vendor))
                    .Select(i => new GpuDevice(i.Handle))
                    .ToList();
            }
            catch (TypeInitializationException)
            {
                // ignore missing opencl implementation
            }
        }

        /// <summary>
        /// All miners exposed by this factory.
        /// </summary>
        IEnumerable<GpuMiner> miners;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        [ImportingConstructor]
        public GpuMinerFactory([Import] IMinerContext context)
        {
            miners = gpus
                .Select(i => new GpuMiner(context, i))
                .ToList();
        }

        /// <summary>
        /// Gets the GPU resources available.
        /// </summary>
        public IEnumerable<MinerDevice> Devices
        {
            get { return gpus ?? Enumerable.Empty<MinerDevice>(); }
        }

        /// <summary>
        /// Gets the GPU miners available.
        /// </summary>
        public IEnumerable<IMiner> Miners
        {
            get { return miners; }
        }

    }

}
