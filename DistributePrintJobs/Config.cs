using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace DistributePrintJobs
{
    public static class Config
    {
        public static int HttpListenPort { get; set; }

        public static void LoadConfig()
        {
            Dictionary<string, object> configDict;

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
