using System;
using System.ComponentModel.Composition;

namespace BitMaker.Miner
{

    /// <summary>
    /// Marks a <see cref="IMiner"/> as a plugin that should be loaded.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class MinerAttribute : ExportAttribute, IMinerMetadata
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public MinerAttribute()
            : base(typeof(IMiner))
        {

        }

    }

    /// <summary>
    /// Metadata exported by a miner implementation.
    /// </summary>
    public interface IMinerMetadata
    {



    }

}
