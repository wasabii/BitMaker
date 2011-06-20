using System;
using System.Configuration;

namespace BitMaker.Miner
{

    public class ConfigurationSection : System.Configuration.ConfigurationSection
    {

        /// <summary>
        /// Gets the default configuration section for the miner.
        /// </summary>
        /// <returns></returns>
        public static ConfigurationSection GetDefaultSection()
        {
            return (ConfigurationSection)ConfigurationManager.GetSection("bitmaker.miner") ?? new ConfigurationSection();
        }

        /// <summary>
        /// Gets or sets the JSON-RPC interface endpoint.
        /// </summary>
        [ConfigurationProperty("url")]
        public Uri Url
        {
            get { return (Uri)this["url"]; }
            set { this["url"] = value; }
        }

    }

}
