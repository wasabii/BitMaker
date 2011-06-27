using System;
using System.ComponentModel.Composition;

namespace BitMaker.Miner
{

    /// <summary>
    /// Marks a <see cref="IMinerFactory"/> as capable of generating miner instances.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class MinerFactoryAttribute : ExportAttribute, IMinerFactoryMetadata
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public MinerFactoryAttribute()
            : base(typeof(IMinerFactory))
        {

        }

    }

    /// <summary>
    /// Metadata exported by a miner factory implementation.
    /// </summary>
    public interface IMinerFactoryMetadata
    {



    }

}
