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

        public Engine Engine { get; private set; }

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
            Engine = new Engine();

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
            // current milliseconds
            var now = GetTickCount();

            // last millisecond value of input
            var str = new LASTINPUTINFO();
            str.cbSize = (uint)Marshal.SizeOf(str);
            GetLastInputInfo(ref str);
            var lastInput = str.dwTime;

            // derive the timespan since the system went idle
            var idleTime = new TimeSpan(0, 0, 0, 0, (int)now - (int)lastInput);
            
            if (idle && idleTime < TimeSpan.FromSeconds(10))
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

        [DllImport("kernel32.dll")]
        private static extern uint GetTickCount();

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        internal struct LASTINPUTINFO
        {

            public uint cbSize;

            public uint dwTime;

        }

    }

}