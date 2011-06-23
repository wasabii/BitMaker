using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

using BitMaker.Miner;

namespace BitMaker
{

    public partial class App : Application
    {

        private static TimeSpan idleThreshold = ConfigurationSection.GetDefaultSection().IdleThreshold;

        public MinerHost Engine { get; private set; }

        /// <summary>
        /// Determines whether to start or stop the engine.
        /// </summary>
        private DispatcherTimer timer;

        /// <summary>
        /// Reference to the view driving the miner window.
        /// </summary>
        private MinerWindowViewModel view;

        /// <summary>
        /// If <c>true</c> a monitor window is shown.
        /// </summary>
        private bool showWindow;

        /// <summary>
        /// If <c>true</c> the engine only runs when the system is idle.
        /// </summary>
        private bool idle;

        /// <summary>
        /// Invoked when the application starts.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void Application_Startup(object sender, StartupEventArgs args)
        {
            // read command arguments
            showWindow = !args.Args.Any(i => i == "/hidden");
            idle = !args.Args.Any(i => i == "/run");

            // miner engine
            Engine = new MinerHost();

            // if not hidden, show monitor window
            if (showWindow)
            {
                var window = new MinerWindow();
                window.DataContext = view = new MinerWindowViewModel() { Engine = Engine };
                window.Show();
            }

            // ticks periodically
            timer = new DispatcherTimer(DispatcherPriority.Normal);
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += timer_Tick;
            timer.Start();
        }

        /// <summary>
        /// Invoked periodically.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void timer_Tick(object sender, EventArgs args)
        {
            // derive the timespan since the system went idle
            var idleFor = new TimeSpan(0, 0, 0, 0, (int)GetTickCount() - (int)GetLastInputTickCount());

            // check for idleness and start or stop the engine
            if (idle && idleFor < idleThreshold)
                Engine.Stop();
            else
                Engine.Start();
        }

        /// <summary>
        /// Invoked when the application is exiting.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void Application_Exit(object sender, ExitEventArgs args)
        {
            Engine.Stop();
        }

        /// <summary>
        /// Obtains the current tick.
        /// </summary>
        /// <returns></returns>
        [DllImport("kernel32.dll")]
        private static extern uint GetTickCount();

        /// <summary>
        /// Obtains the tick the system last received input.
        /// </summary>
        /// <param name="plii"></param>
        /// <returns></returns>
        [DllImport("user32.dll", EntryPoint="GetLastInputInfo")]
        private static extern bool _GetLastInputInfo(ref LASTINPUTINFO plii);

        /// <summary>
        /// Internal structure to store the results of GetLastInputInfo.
        /// </summary>
        private struct LASTINPUTINFO
        {

            public uint cbSize;

            public uint dwTime;

        }

        /// <summary>
        /// Obtains the tick at which the system last received input.
        /// </summary>
        /// <returns></returns>
        private uint GetLastInputTickCount()
        {
            // structure to hold result
            var str = new LASTINPUTINFO();
            str.cbSize = (uint)Marshal.SizeOf(str);

            // invoke native function
            _GetLastInputInfo(ref str);

            // extract and return result
            return str.dwTime;
        }

    }

}