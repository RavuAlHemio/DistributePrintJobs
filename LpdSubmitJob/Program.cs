using System;
using System.IO;
using DistributePrintJobs;

namespace LpdSubmitJob
{
    static class Program
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
            var jobInfo = new JobInfo
            {
                DataFilePath = localFilename,
                DataFileSize = fileSize,
                DocumentName = remoteFilename,
                HostName = localHostname,
                Status = JobInfo.JobStatus.ReadyToPrint,
                TimeOfArrival = DateTimeOffset.Now,
                UserName = user
            };

            // prepare the sender
            var sender = new LpdSender
            {
                Host = host,
                Port = port,
                QueueName = queue
            };
            sender.Send(jobInfo);

            Console.WriteLine("Job sent.");
        }
    }
}
