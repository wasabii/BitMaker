using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Cloo;
using Cloo.Bindings;

namespace BitMaker.Miner.Gpu
{

    /// <summary>
    /// Represents a single GPU in the system.
    /// </summary>
    public class GpuDevice : MinerDevice
    {

        /// <summary>
        /// Device names that support BFI_INT.
        /// </summary>
        static readonly string[] BFI_INT_DEVICES = new string[]
        {
			"Cedar",
            "Redwood",
            "Juniper",
            "Cypress",
            "Hemlock",
            "Caicos",
            "Turks",
            "Barts",
            "Cayman",
            "Antilles",
            "Palm",
            "Sumo",
            "Wrestler",
            "WinterPark",
            "BeaverCreek",
	    };

        static readonly string[] UPPER = { "X", "Y", "Z", "W", "T", "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "k" };
        static readonly string[] LOWER = { "x", "y", "z", "w", "t", "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k" };

        /// <summary>
        /// Gets the raw GPU source.
        /// </summary>
        /// <returns></returns>
        static string GetRawSource()
        {
            using (var rdr = new StreamReader(typeof(GpuDevice).Assembly.GetManifestResourceStream("BitMaker.Miner.Gpu.DiabloMiner.cl")))
                return rdr.ReadToEnd();
        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="clDeviceHandle"></param>
        public GpuDevice(CLDeviceHandle clDeviceHandle)
        {
            CLDeviceHandle = clDeviceHandle;
            CLDevice = ComputePlatform.Platforms
                .SelectMany(i => i.Devices)
                .SingleOrDefault(i => i.Handle.Value == CLDeviceHandle.Value);
        }

        public override string Id
        {
            get { return CLDevice.Name; }
        }

        /// <summary>
        /// Handle to the CLDevice.
        /// </summary>
        public CLDeviceHandle CLDeviceHandle { get; private set; }

        /// <summary>
        /// Gets the OpenCL <see cref="ComputeDevice"/>.
        /// </summary>
        public ComputeDevice CLDevice { get; private set; }

        /// <summary>
        /// Gets the maximum number of work-items in a work-group.
        /// </summary>
        public long WorkSize
        {
            get { return CLDevice.MaxWorkGroupSize; }
        }

        public bool HasBitAlign
        {
            get { return CLDevice.Extensions.Contains("cl_amd_media_ops"); }
        }

        public bool HasBfiInt
        {
            get { return false && HasBitAlign && BFI_INT_DEVICES.Any(i => CLDevice.Name.Contains(i)); }
        }

        /// <summary>
        /// Gets the OpenCL source code for the device.
        /// </summary>
        /// <returns></returns>
        public string GetSource()
        {
            var sourceLines = GetRawSource().Split('\n');
            var source = "";

            long vectorBase = 0;
            var vectors = new[] { 1 };
            var totalVectors = 0;
            int totalVectorsPOT;

            foreach (var vector in vectors)
                totalVectors += vector;

            if (totalVectors > 16)
                throw new Exception("Does not support more than 16 total vectors yet");

            int powtwo = 1 << (32 - 31 - 1);

            if (totalVectors != powtwo)
                totalVectorsPOT = 1 << (32 - 31);
            else
                totalVectorsPOT = totalVectors;

            for (int x = 0; x < sourceLines.Length; x++)
            {
                String sourceLine = sourceLines[x];

                if (false /* diabloMiner.getGPUNoArray() */ && !sourceLine.Contains("z ZA"))
                {
                    sourceLine = Regex.Replace(sourceLine, "ZA\\[([0-9]+)\\]", "ZA$1");
                }

                if (sourceLine.Contains("zz"))
                {
                    if (totalVectors > 1)
                        sourceLine = sourceLine.Replace("zz", totalVectorsPOT.ToString());
                    else
                        sourceLine = sourceLine.Replace("zz", "");
                }

                if (sourceLine.Contains("= (io) ? Znonce"))
                {
                    int count = 0;
                    String change = "(uintzz)(";

                    for (int z = 0; z < vectors.Length; z++)
                    {
                        change += UPPER[z] + "nonce";
                        count += vectors[z];

                        if (z != vectors.Length - 1)
                            change += ", ";
                    }

                    for (int z = count; z < totalVectorsPOT; z++)
                        change += ", 0";

                    change += ")";

                    sourceLine = sourceLine.Replace("Znonce", change);

                    if (totalVectors > 1)
                        sourceLine = sourceLine.Replace("zz", totalVectorsPOT.ToString());
                    else
                        sourceLine = sourceLine.Replace("zz", "");

                    source += sourceLine + "\n";
                }
                else if ((sourceLine.Contains("Z") || sourceLine.Contains("z")) && !sourceLine.Contains("__"))
                {
                    for (int y = 0; y < vectors.Length; y++)
                    {
                        String replace = sourceLine;

                        if (false /*diabloMiner.getGPUNoArray() */ && replace.Contains("z ZA"))
                        {
                            replace = "";

                            for (int z = 0; z < 930; z += 5)
                            {
                                replace += "		 ";

                                for (int w = 0; w < 5; w++)
                                    replace += "z ZA" + (z + w) + "; ";

                                replace += "\n";
                            }
                        }

                        if (vectors[y] > 1 && replace.Contains("typedef"))
                        {
                            replace = replace.Replace("uint", "uint" + vectors[y]);
                        }
                        else if (replace.Contains("z Znonce"))
                        {
                            String vectorGlobal;

                            if (vectors[y] > 1)
                                vectorGlobal = " + (uint" + vectors[y] + ")(";
                            else
                                vectorGlobal = " + (uint)(";

                            for (int i = 0; i < vectors[y]; i++)
                            {
                                vectorGlobal += (vectorBase + i).ToString();

                                if (i != vectors[y] - 1)
                                    vectorGlobal += ", ";
                            }

                            vectorGlobal += ");";

                            replace = replace.Replace(";", vectorGlobal);

                            vectorBase += vectors[y];
                        }

                        if (vectors[y] == 1 && replace.Contains("bool Zio"))
                        {
                            replace = replace.Replace("any(", "(");
                        }

                        source += replace.Replace("Z", UPPER[y]).Replace("z", LOWER[y]) + "\n";
                    }
                }
                else if (totalVectors == 1 && sourceLine.Contains("any(nonce"))
                {
                    source += sourceLine.Replace("any", "") + "\n";
                }
                else if (sourceLine.Contains("__global"))
                {
                    if (totalVectors > 1)
                        source += sourceLine.Replace("uint", "uint" + totalVectorsPOT) + "\n";
                    else
                        source += sourceLine + "\n";
                }
                else
                {
                    source += sourceLine + "\n";
                }
            }

            return source;
        }

    }

}
