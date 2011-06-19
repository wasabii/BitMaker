using System;
using System.Threading;

namespace BitMaker.Miner.Plugin
{

    /// <summary>
    /// Unit of work delivered to a plugin. Plugins have exclusive access to this work and can modify the header value
    /// before submitting it back to the context.
    /// </summary>
    public sealed class Work
    {

        /// <summary>
        /// Gets a token that will signal when the work should no longer be processed.
        /// </summary>
        public CancellationToken Token { get; set; }

        /// <summary>
        /// Gets or sets the header data.
        /// </summary>
        public byte[] Header { get; set; }

        /// <summary>
        /// Gets or sets the target data.
        /// </summary>
        public byte[] Target { get; set; }

    }

}
