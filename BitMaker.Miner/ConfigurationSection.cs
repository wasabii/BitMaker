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
            return (ConfigurationSection)ConfigurationManager.GetSection("bitMaker.miner") ?? new ConfigurationSection();
        }
        
        [ConfigurationProperty("miners", IsDefaultCollection = false)]
        [ConfigurationCollection(typeof(MinerFactorysConfigurationCollection))]
        public MinerFactorysConfigurationCollection Miners
        {
            get { return (MinerFactorysConfigurationCollection)this["miners"]; }
        }

        [ConfigurationProperty("pools", IsDefaultCollection = false)]
        [ConfigurationCollection(typeof(PoolsConfigurationCollection))]
        public PoolsConfigurationCollection Pools
        {
            get { return (PoolsConfigurationCollection)this["pools"]; }
        }

    }

}
