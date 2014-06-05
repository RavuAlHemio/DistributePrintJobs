using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace DistributePrintJobs
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
#if SERVICE_MODE
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[] 
            { 
                new Service1() 
            };
            ServiceBase.Run(ServicesToRun);
#else
            var prtSender = new LpdSender();
            prtSender.QueueName = "LOL";
            prtSender.Address = IPAddress.Parse("127.0.0.1");
            var prt = new PrinterInfo();
            prt.ShortName = "one";
            prt.Sender = prtSender;
            Management.AddPrinter(prt);

            var lpdListener = new LpdListener();
            lpdListener.NewJobReceived += (sender, newJobInfo) =>
            {
                Management.AddJob(newJobInfo);
            };
            lpdListener.Start();

            var httpListener = new HttpListener();
            httpListener.Start();
#endif
        }
    }
}
