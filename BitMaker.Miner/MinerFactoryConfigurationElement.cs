using System;
using System.ComponentModel;
using System.Configuration;

namespace BitMaker.Miner
{

    /// <summary>
    /// Represents the configuration for a single miner factory.
    /// </summary>
    public class MinerFactoryConfigurationElement : ConfigurationElement
    {

        /// <summary>
        /// Gets or sets the Type of the miner factory.
        /// </summary>
        [ConfigurationProperty("type", IsRequired = true)]
        [CallbackValidator(Type = typeof(MinerFactoryConfigurationElement), CallbackMethodName = "TypeValidator")]
        [TypeConverter(typeof(TypeNameConverter))]
        public Type Type
        {
            get { return (Type)this["type"]; }
            set { this["type"] = value; }
        }

        public static void TypeValidator(object value)
        {
            var type = (Type)value;
            if (!typeof(IMinerFactory).IsAssignableFrom(type))
                throw new ArgumentException("Type of miner factory must extend BitMaker.Miner.IMinerFactory.");
        }

    }

}
