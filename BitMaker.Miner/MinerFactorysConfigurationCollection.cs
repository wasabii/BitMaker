using System.Collections.Generic;
using System.Configuration;
using System.Linq;

namespace BitMaker.Miner
{

    public class MinerFactorysConfigurationCollection : ConfigurationElementCollection, IEnumerable<MinerFactoryConfigurationElement>
    {

        protected override ConfigurationElement CreateNewElement()
        {
            return new MinerFactoryConfigurationElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((MinerFactoryConfigurationElement)element).Type;
        }

        public new IEnumerator<MinerFactoryConfigurationElement> GetEnumerator()
        {
            var e = base.GetEnumerator();
            while (e.MoveNext())
                yield return (MinerFactoryConfigurationElement)e.Current;
        }

    }

}
