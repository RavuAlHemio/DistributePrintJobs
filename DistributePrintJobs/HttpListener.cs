using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DotLiquid;
using Newtonsoft.Json;

namespace DistributePrintJobs
{
    class HttpListener
    {
        private System.Net.HttpListener Listener { get; set; }
        private bool StopNow { get; set; }
        private Dictionary<string, Template> TemplateCache { get; set; }

        private static Dictionary<string, string> ExtensionToMimeType = new Dictionary<string, string>()
        {
            { ".css", "text/css" },
            { ".js", "text/javascript" }
        };

        class JobInfoDrop : Drop
        {
            private readonly JobInfo Info;

            public JobInfoDrop(JobInfo info)
            {
                Info = info;
            }

            public int StatusCode { get { return (int)Info.Status; } }
            public string JobId { get { return Info.JobID.ToString(); } }
            public string TimeOfArrival { get { return Info.TimeOfArrival.ToString("dd. MM. yyyy HH:mm:ss"); } }
            public string HostName { get { return Info.HostName; } }
            public string UserName { get { return Info.UserName; } }
            public string DocumentName { get { return Info.DocumentName; } }
            public string TargetPrinterShortName { get { return Info.TargetPrinterID.HasValue ? Management.Printers[Info.TargetPrinterID.Value].ShortName : "???"; } }
        }

        class PrinterInfoDrop : Drop
        {
            private readonly PrinterInfo Info;

            public PrinterInfoDrop(PrinterInfo info)
            {
                Info = info;
            }

            public uint PrinterId { get { return Info.PrinterID; } }
            public string ShortName { get { return Info.ShortName; } }
        }

        public HttpListener()
        {
            Listener = new System.Net.HttpListener();
            Listener.Prefixes.Add("http://+:8080/");

            Template.FileSystem = new DotLiquid.FileSystems.LocalFileSystem(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates"));
            
            TemplateCache = new Dictionary<string, Template>();
            using (var r = new StreamReader(Path.Combine("Templates", "jobs.liquid")))
            {
                TemplateCache.Add("jobs", Template.Parse(r.ReadToEnd()));
            }
        }

        public void Start()
        {
            StopNow = false;
            Listener.Start();
            Listener.BeginGetContext(new AsyncCallback(ListenerCallback), Listener);
        }

        public void Stop()
        {
            StopNow = true;
            Listener.Stop();
        }

        private void SendOk(System.Net.HttpListenerResponse response, string mimeType, byte[] body)
        {
            response.StatusCode = 200;
            response.StatusDescription = "OK";
            response.ContentType = mimeType;

            response.ContentLength64 = body.LongLength;
            response.OutputStream.Write(body, 0, body.Length);
            response.OutputStream.Close();
        }

        private void SendOkHtml(System.Net.HttpListenerResponse response, string htmlText)
        {
            SendOk(response, "text/html; charset=utf-8", Encoding.UTF8.GetBytes(htmlText));
        }

        private void SendOkJson(System.Net.HttpListenerResponse response)
        {
            SendOk(response, "application/json", Encoding.UTF8.GetBytes("{ \"status\": \"success\" }"));
        }

        private void SendError(System.Net.HttpListenerResponse response, int code, string description, string body)
        {
            response.StatusCode = code;
            response.StatusDescription = description;
            response.ContentType = "application/json";

            var retDict = new Dictionary<string, string>();
            retDict["status"] = "error";
            retDict["error"] = body;
            var jsonString = JsonConvert.SerializeObject(retDict);

            var textBytes = Encoding.UTF8.GetBytes(jsonString);
            response.ContentLength64 = textBytes.LongLength;
            response.OutputStream.Write(textBytes, 0, textBytes.Length);
            response.OutputStream.Close();
        }

        private void Send400MissingParameter(System.Net.HttpListenerResponse response)
        {
            SendError(response, 400, "Bad Request", "Missing parameter!");
        }

        private void Send400MalformedParameter(System.Net.HttpListenerResponse response)
        {
            SendError(response, 400, "Bad Request", "Malformed parameter!");
        }

        private void Send400ParameterResourceNonexistent(System.Net.HttpListenerResponse response)
        {
            SendError(response, 400, "Bad Request", "The requested resource does not exist!");
        }

        private void Send404(System.Net.HttpListenerResponse response)
        {
            SendError(response, 404, "Not Found", "Not found!");
        }

        private void ListenerCallback(IAsyncResult result)
        {
            // fetch the context
            var context = Listener.EndGetContext(result);

            // get ready for the next request
            Listener.BeginGetContext(new AsyncCallback(ListenerCallback), Listener);

            if (context.Request.HttpMethod == "GET")
            {
                if (context.Request.Url.AbsolutePath == "/jobs")
                {
                    var variables = new Hash();
                    variables.Add("jobs", Management.Jobs.Values.OrderBy((k) => k.TimeOfArrival).Reverse().Select((j) => new JobInfoDrop(j)).ToArray());
                    variables.Add("printers", Management.Printers.Values.OrderBy((k) => k.PrinterID).Select((p) => new PrinterInfoDrop(p)).ToArray());
                    var rendered = TemplateCache["jobs"].Render(variables);
                    SendOkHtml(context.Response, rendered);
                }
                else if (context.Request.Url.AbsolutePath.StartsWith("/static/"))
                {
                    var path = context.Request.Url.AbsolutePath.Substring(("/static/").Length);
                    if (path.Contains("/"))
                    {
                        Send404(context.Response);
                    }
                    else
                    {
                        var mimeType = "application/octet-stream";
                        if (path.Contains('.'))
                        {
                            var ext = path.Substring(path.LastIndexOf('.'));
                            if (ExtensionToMimeType.ContainsKey(ext))
                            {
                                mimeType = ExtensionToMimeType[ext];
                            }
                        }

                        try
                        {
                            using (var s = new FileStream(Path.Combine("Static", path), FileMode.Open))
                            {
                                SendOk(context.Response, mimeType, BinaryStreamReader.ReadStreamToEnd(s));
                            }
                        }
                        catch (FileNotFoundException)
                        {
                            Send404(context.Response);
                        }
                    }
                }
                else
                {
                    Send404(context.Response);
                }
            }
            else if (context.Request.HttpMethod == "POST")
            {
                if (context.Request.Url.AbsolutePath == "/dojob")
                {
                    // get the parameters
                    var parameterBody = BinaryStreamReader.ReadStreamToEnd(context.Request.InputStream);

                    // decode them
                    var parameterString = Encoding.Default.GetString(parameterBody);

                    // split them up
                    var parameters = Util.DecodeUriParameters(parameterString);

                    if (!parameters.ContainsKey("do") || !parameters.ContainsKey("jobID"))
                    {
                        Send400MissingParameter(context.Response);
                        return;
                    }

                    ulong jobID;
                    if (!ulong.TryParse(parameters["jobID"], out jobID))
                    {
                        Send400MalformedParameter(context.Response);
                        return;
                    }

                    var jobs = Management.Jobs;
                    if (!jobs.ContainsKey(jobID))
                    {
                        Send400ParameterResourceNonexistent(context.Response);
                        return;
                    }

                    var doParam = parameters["do"];
                    if (doParam == "sendJobToPrinter")
                    {
                        if (!parameters.ContainsKey("printerID"))
                        {
                            Send400MissingParameter(context.Response);
                            return;
                        }

                        uint printerID;
                        if (!uint.TryParse(parameters["printerID"], out printerID))
                        {
                            Send400MalformedParameter(context.Response);
                            return;
                        }

                        var printers = Management.Printers;
                        if (!printers.ContainsKey(printerID))
                        {
                            Send400ParameterResourceNonexistent(context.Response);
                            return;
                        }

                        // send!!
                        printers[printerID].Sender.Send(jobs[jobID]);
                        jobs[jobID].Status = JobInfo.JobStatus.SentToPrinter;
                        jobs[jobID].TargetPrinterID = printerID;
                    }
                    else if (doParam == "removeJob")
                    {
                        // remove it
                        Management.RemoveJob(jobID);
                    }
                    else if (doParam == "resetJob")
                    {
                        if (Management.Jobs[jobID].Status == JobInfo.JobStatus.SentToPrinter)
                        {
                            Management.Jobs[jobID].Status = JobInfo.JobStatus.ReadyToPrint;
                        }
                    }
                    else
                    {
                        Send404(context.Response);
                        return;
                    }

                    SendOkJson(context.Response);
                }
            }
            else
            {
                Send404(context.Response);
                return;
            }
        }
    }
}
