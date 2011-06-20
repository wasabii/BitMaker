using System;
using System.Configuration;

namespace BitMaker
{

    public class ConfigurationSection : System.Configuration.ConfigurationSection
    {

        /// <summary>
        /// Gets the default configuration section.
        /// </summary>
        /// <returns></returns>
        public static ConfigurationSection GetDefaultSection()
        {
            return (ConfigurationSection)ConfigurationManager.GetSection("bitmaker") ?? new ConfigurationSection();
        }

        /// <summary>
        /// Gets or sets the amount of time that the user must have no actions for, before starting the engine.
        /// </summary>
        [ConfigurationProperty("idleThreshold", DefaultValue = "00:10:00")]
        public TimeSpan IdleThreshold
        {
            get { return (TimeSpan)this["idleThreshold"]; }
            set { this["idleThreshold"] = value; }
        }

    }

}
