using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DistributePrintJobs;

namespace LpdSubmitJob
{
    class Program
    {
        static void UsageAndExit()
        {
            Console.Error.WriteLine(
                "Usage: {0} HOST[:PORT] QUEUE USER FILENAME [FAKEFILENAME [FAKEHOSTNAME]]",
                AppDomain.CurrentDomain.FriendlyName
            );
            Environment.Exit(1);
        }

        static void Main(string[] args)
        {
            // parse args
            if (args.Length < 4 || args.Length > 6)
            {
                UsageAndExit();
            }

            var pseudoUri = new Uri(string.Format("lpd://{0}", args[0]));
            var host = pseudoUri.Host;
            var port = 515;
            if (!pseudoUri.IsDefaultPort)
            {
                port = pseudoUri.Port;
            }

            var queue = args[1];
            var user = args[2];
            var localFilename = args[3];
            var remoteFilename = (args.Length > 4) ? args[4] : Path.GetFileName(localFilename);
            var localHostname = (args.Length > 5) ? args[5] : System.Net.Dns.GetHostName().Split('.')[0];
            
            // sanity check
            var fileInfo = new FileInfo(localFilename);
            if (!fileInfo.Exists)
            {
                Console.Error.WriteLine("Local file '{0}' does not exist!", localFilename);
                UsageAndExit();
            }
            var fileSize = fileInfo.Length;

            // prepare the JobInfo
            var jobInfo = new JobInfo();
            jobInfo.DataFilePath = localFilename;
            jobInfo.DataFileSize = fileSize;
            jobInfo.DocumentName = remoteFilename;
            jobInfo.HostName = localHostname;
            jobInfo.Status = JobInfo.JobStatus.ReadyToPrint;
            jobInfo.TimeOfArrival = DateTimeOffset.Now;
            jobInfo.UserName = user;

            // prepare the sender
            var sender = new LpdSender();
            sender.Host = host;
            sender.Port = port;
            sender.QueueName = queue;
            sender.Send(jobInfo);

            Console.WriteLine("Job sent.");
        }
    }
}
