using System;
using System.Threading;
using DistributePrintJobs;

namespace DistributePrintJobsCLI
{
    static class Program
    {
        static Semaphore StopSemaphore;

        private static void Stop(object sender, ConsoleCancelEventArgs ea)
        {
            StopSemaphore.Release();
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            Util.SetupLogging();

            StopSemaphore = new Semaphore(0, 1);
            Console.CancelKeyPress += Stop;

            Config.LoadConfig();
            Management.ReadJobs();

            var lpdListener = new LpdListener();
            lpdListener.NewJobReceived += (sender, newJobInfo) =>
            {
                Management.AddJob(newJobInfo);
            };
            lpdListener.Start();

            var httpListener = new DistributePrintJobs.HttpListener(Config.HttpListenPort);
            httpListener.Start();

            // wait
            StopSemaphore.WaitOne();

            httpListener.Stop();
            lpdListener.Stop();
        }
    }
}
