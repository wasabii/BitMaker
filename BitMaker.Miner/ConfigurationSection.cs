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

        [ConfigurationProperty("pools", IsDefaultCollection = false)]
        [ConfigurationCollection(typeof(PoolsConfigurationCollection))]
        public PoolsConfigurationCollection Pools
        {
            get { return (PoolsConfigurationCollection)this["pools"]; }
        }

    }

}
