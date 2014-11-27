// Released into the public domain.
// http://creativecommons.org/publicdomain/zero/1.0/

using System;
using System.IO;
using System.Linq;
using System.Text;
using log4net;
using Newtonsoft.Json.Linq;

namespace DistributePrintJobs
{
    public static class Config
    {
        private static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static int HttpListenPort { get; set; }

        public static int LpdListenPort { get; set; }

        public static string JobDirectory { get; set; }

        public static int[] DeletionAgeMinutesOptions { get; set; }

        public static void LoadConfig()
        {
            JObject jobject;

            Logger.Info("loading config");

            // set up defaults
            HttpListenPort = 8080;
            LpdListenPort = 515;
            JobDirectory = Path.Combine(Util.ProgramDirectory, "Jobs");
            DeletionAgeMinutesOptions = new [] { 15, 30, 60, 120 };

            using (var r = new StreamReader(new FileStream(Path.Combine(Util.ProgramDirectory, "Config.json"), FileMode.Open, FileAccess.Read), Encoding.UTF8))
            {
                jobject = JObject.Parse(r.ReadToEnd());
            }

            if (jobject["HttpListenPort"] != null)
            {
                HttpListenPort = (int)jobject["HttpListenPort"];
            }

            if (jobject["LpdListenPort"] != null)
            {
                LpdListenPort = (int)jobject["LpdListenPort"];
            }

            if (jobject["JobDirectory"] != null)
            {
                JobDirectory = (string)jobject["JobDirectory"];
                if (!Path.IsPathRooted(JobDirectory))
                {
                    JobDirectory = Path.Combine(Util.ProgramDirectory, JobDirectory);
                }
            }

            if (jobject["DeletionAgeMinutesOptions"] != null)
            {
                DeletionAgeMinutesOptions = jobject["DeletionAgeMinutesOptions"].Select(opt => (int) opt).ToArray();
            }

            if (jobject["Printers"] != null)
            {
                foreach (JObject printer in jobject["Printers"])
                {
                    var shortName = (string)printer["ShortName"];
                    var connection = (string)printer["Connection"];

                    uint distributionFactor = 1;
                    if (printer["DistributionFactor"] != null)
                    {
                        distributionFactor = (uint)printer["DistributionFactor"];
                    }

                    ISender sender;
                    if (connection == "LPD")
                    {
                        var lpdSender = new LpdSender
                        {
                            Host = (string) printer["Host"],
                            QueueName = (string) printer["Queue"]
                        };
                        if (printer["Port"] != null)
                        {
                            lpdSender.Port = (int)printer["Port"];
                        }
                        sender = lpdSender;
                    }
                    else
                    {
                        throw new ArgumentException("unknown printer connection '" + connection + "'");
                    }

                    var printerInfo = new PrinterInfo
                    {
                        DistributionFactor = distributionFactor,
                        ShortName = shortName,
                        Sender = sender
                    };
                    Management.AddPrinter(printerInfo);
                }
            }
        }
    }
}
