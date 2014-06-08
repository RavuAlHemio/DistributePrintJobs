using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using log4net;
using Newtonsoft.Json;

namespace DistributePrintJobs
{
    public static class Config
    {
        private static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static int HttpListenPort { get; set; }

        public static void LoadConfig()
        {
            Dictionary<string, object> configDict;

            Logger.Info("loading config");

            // set up defaults
            HttpListenPort = 8080;

            using (var r = new StreamReader(new FileStream("Config.json", FileMode.Open), Encoding.UTF8))
            {
                configDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(r.ReadToEnd());
            }

            if (configDict.ContainsKey("HttpListenPort"))
            {
                HttpListenPort = (int)configDict["HttpListenPort"];
            }

            if (configDict.ContainsKey("Printers"))
            {
                var printers = configDict["Printers"] as List<Dictionary<string, object>>;
                foreach (var printer in printers)
                {
                    var shortName = printer["ShortName"] as string;
                    var connection = printer["Connection"] as string;
                    ISender sender;
                    if (connection == "LPD")
                    {
                        var lpdSender = new LpdSender();
                        lpdSender.Host = printer["Host"] as string;
                        lpdSender.QueueName = printer["Queue"] as string;
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
