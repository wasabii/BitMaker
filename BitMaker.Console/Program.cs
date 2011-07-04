using System.Threading;

using BitMaker.Miner;

namespace BitMaker.Console
{

    public static class Program
    {

        private static MinerHost engine;

        public static void Main(string[] args)
        {
            engine = new MinerHost();
            engine.Start();

            global::System.Console.WriteLine();
            global::System.Console.WriteLine();

            global::System.Console.WriteLine();
            global::System.Console.WriteLine();

            // render hash statistics
            var timer = new Timer(TimerCallback, null, 0, 250);

            // wait for input from user to terminate
            global::System.Console.ReadLine();
            timer.Dispose();
            engine.Stop();

            global::System.Console.WriteLine("Total Hashes: {0}", engine.HashCount);
            global::System.Console.ReadLine();
        }

        private static void TimerCallback(object state)
        {
            global::System.Console.CursorLeft = 0;
            global::System.Console.CursorTop -= 1;
            global::System.Console.WriteLine("{0,10:0,000} khash/s              ", engine.HashesPerSecond / 1000);
        }

    }

}
