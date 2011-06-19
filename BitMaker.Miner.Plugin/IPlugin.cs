
namespace BitMaker.Miner.Plugin
{

    /// <summary>
    /// Interface required by plugin implementations.
    /// </summary>
    public interface IPlugin
    {

        /// <summary>
        /// Starts execution of the plugin.
        /// </summary>
        void Start(IPluginContext context);

        /// <summary>
        /// Stops execution of the plugin.
        /// </summary>
        void Stop();

    }

}
