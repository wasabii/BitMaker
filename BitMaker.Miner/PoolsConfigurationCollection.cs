using System.Configuration;

namespace BitMaker.Miner
{

    public class PoolsConfigurationCollection : ConfigurationElementCollection
    {

        protected override ConfigurationElement CreateNewElement()
        {
            return new PoolConfigurationElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((PoolConfigurationElement)element).Url;
        }

    }

}
