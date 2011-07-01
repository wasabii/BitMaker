using System.Collections.Generic;
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

        /// <summary>
        /// All available GPUs in the system.
        /// </summary>
        public static readonly IEnumerable<MinerResource> gpuResources = global::Cloo.ComputePlatform.Platforms
                .SelectMany(i => i.Devices)
                .Select(i => new GpuResource()
                {
                    Id = i.Name,
                    CLDeviceHandle = i.Handle,
                })
                .ToList();

        /// <summary>
        /// <see cref="T:GpuMiner"/> can consume all of the available OpenCL GPUs in the system.
        /// </summary>
        public IEnumerable<MinerResource> Resources
        {
            get { return gpuResources; }
        }

        /// <summary>
        /// Invoked by the host to begin a new miner.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="resource"></param>
        /// <returns></returns>
        public IMiner StartMiner(IMinerContext context, MinerResource resource)
        {
            var miner = new GpuMiner(context, (GpuResource)resource);
            miner.Start();

            return miner;
        }

    }

}
