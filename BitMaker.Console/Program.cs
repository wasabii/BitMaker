using System;
using System.Threading;

using BitMaker.Miner;

namespace BitMaker.Console
{

    public static class Program
    {

        private static Engine engine;

        public static void Main(string[] args)
        {
            engine = new Engine();
            engine.Start();

            global::System.Console.WriteLine();
            global::System.Console.WriteLine();

            // render hash statistics
            var timer = new Timer(TimerCallback, null, 0, 2000);

            // wait for input from user to terminate
            global::System.Console.ReadLine();
            timer.Dispose();
            engine.Stop();
        }

        private static void TimerCallback(object state)
        {
            global::System.Console.CursorLeft = 1;
            global::System.Console.CursorTop -= 1;
            global::System.Console.WriteLine("{0,10:0,000} khash/s", engine.HashesPerSecond / 1000);
        }

    }

}
