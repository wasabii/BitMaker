
namespace BitMaker.Miner.Plugin
{

    /// <summary>
    /// Interface required by plugin implementations.
    /// </summary>
    public interface IMiner
    {

        /// <summary>
        /// Starts execution of the plugin.
        /// </summary>
        void Start(IMinerContext context);

        /// <summary>
        /// Stops execution of the plugin.
        /// </summary>
        void Stop();

    }

}
