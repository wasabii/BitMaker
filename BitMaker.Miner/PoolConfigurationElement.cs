using System;
using System.Configuration;

namespace BitMaker.Miner
{

    /// <summary>
    /// Represents the configuration for a single pool.
    /// </summary>
    public class PoolConfigurationElement : ConfigurationElement
    {

        /// <summary>
        /// Gets or sets the JSON-RPC interface endpoint.
        /// </summary>
        [ConfigurationProperty("url", IsRequired = true)]
        [CallbackValidator(Type = typeof(PoolConfigurationElement), CallbackMethodName = "UrlValidator")]
        public Uri Url
        {
            get { return (Uri)this["url"]; }
            set { this["url"] = value; }
        }

        public static void UrlValidator(object value)
        {
            var url = (Uri)value;
            if (!url.IsAbsoluteUri)
                throw new ArgumentException("Url of pool must be an absolute uri.");
            else if (url.Scheme != "http" && url.Scheme != "https")
                throw new ArgumentException("Url of pool must be either http or https.");
        }

    }

}
