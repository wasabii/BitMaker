using System.Windows;
using System.Windows.Threading;
using System;

namespace BitMaker
{

    public partial class MinerWindow : Window
    {

        /// <summary>
        /// To monitor the engine.
        /// </summary>
        private DispatcherTimer Timer { get; set; }

        /// <summary>
        /// References to the view model.
        /// </summary>
        public MinerWindowViewModel View { get; private set; }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public MinerWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Invoked when the DataContext is changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void Window_DataContextChanged(object sender, DependencyPropertyChangedEventArgs args)
        {
            View = (MinerWindowViewModel)DataContext;

            Timer = new DispatcherTimer();
            Timer.Interval = TimeSpan.FromSeconds(2);
            Timer.Tick += Timer_Tick;
            Timer.Start();
        }

        /// <summary>
        /// Invoked when the timer ticks.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void Timer_Tick(object sender, EventArgs args)
        {
            if (View != null && View.Engine != null)
                OutputTextBox.AppendText(string.Format("{0,10:0,000}hps\n", View.Engine.HashesPerSecond));
        }

    }

}
