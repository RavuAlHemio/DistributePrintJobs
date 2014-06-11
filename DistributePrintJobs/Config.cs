using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DistributePrintJobs
{
    public static class Config
    {
        private static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static int HttpListenPort { get; set; }

        public static void LoadConfig()
        {
            JObject jobject;

            Logger.Info("loading config");

            // set up defaults
            HttpListenPort = 8080;

            using (var r = new StreamReader(new FileStream(Path.Combine(Util.ProgramDirectory, "Config.json"), FileMode.Open, FileAccess.Read), Encoding.UTF8))
            {
                jobject = JObject.Parse(r.ReadToEnd());
            }

            if (jobject["HttpListenPort"] != null)
            {
                HttpListenPort = (int)jobject["HttpListenPort"];
            }

            if (jobject["Printers"] != null)
            {
                foreach (JObject printer in jobject["Printers"])
                {
                    var shortName = (string)printer["ShortName"];
                    var connection = (string)printer["Connection"];
                    ISender sender;
                    if (connection == "LPD")
                    {
                        var lpdSender = new LpdSender();
                        lpdSender.Host = (string)printer["Host"];
                        lpdSender.QueueName = (string)printer["Queue"];
                        sender = lpdSender;
                    }
                    else
                    {
                        throw new ArgumentException("unknown printer connection '" + connection + "'");
                    }

                    var printerInfo = new PrinterInfo();
                    printerInfo.ShortName = shortName;
                    printerInfo.Sender = sender;
                    Management.AddPrinter(printerInfo);
                }
            }
        }
    }
}
