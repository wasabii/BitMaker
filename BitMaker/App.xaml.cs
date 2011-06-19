using System.Windows;
using BitMaker.Miner;

namespace BitMaker
{

    public partial class App : Application
    {

        public Engine Engine { get; private set; }

        /// <summary>
        /// Invoked when the application starts.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void Application_Startup(object sender, StartupEventArgs args)
        {
            Engine = new Engine();
            Engine.Start();

            var window = new MinerWindow();
            var viewmd = new MinerWindowViewModel() { Engine = Engine };
            window.Show();
            window.DataContext = viewmd;
        }

        /// <summary>
        /// Invoked when the application is exiting.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Application_Exit(object sender, ExitEventArgs e)
        {
            Engine.Stop();
        }

    }

}
