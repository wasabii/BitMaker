using System;
using System.ComponentModel.Composition;

namespace BitMaker.Miner.Plugin
{

    /// <summary>
    /// Marks a <see cref="IPlugin"/> as a plugin that should be loaded.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class PluginAttribute : ExportAttribute, IPluginMetadata
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public PluginAttribute()
            : base(typeof(IPlugin))
        {

        }

    }

    /// <summary>
    /// Metadata exported by a plugin implementation.
    /// </summary>
    public interface IPluginMetadata
    {



    }

}
