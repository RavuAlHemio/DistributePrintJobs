// Released into the public domain.
// http://creativecommons.org/publicdomain/zero/1.0/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DotLiquid;
using log4net;
using Newtonsoft.Json;

namespace DistributePrintJobs
{
    public class HttpListener
    {
        private static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

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
            private readonly string SizeString;

            public JobInfoDrop(JobInfo info)
            {
                Info = info;

                if (info.DataFileSize < 1024L)
                {
                    SizeString = info.DataFileSize + " B";
                }
                else if (info.DataFileSize < 1024L * 1024L)
                {
                    SizeString = (info.DataFileSize / 1024L) + " KB";
                }
                else if (info.DataFileSize < 1024L * 1024L * 1024L)
                {
                    SizeString = (info.DataFileSize / (1024L * 1024L)) + " MB";
                }
                else if (info.DataFileSize < 1024L * 1024L * 1024L * 1024L)
                {
                    SizeString = (info.DataFileSize / (1024L * 1024L * 1024L)) + " GB";
                }
                else
                {
                    SizeString = (info.DataFileSize / (1024L * 1024L * 1024L * 1024L)) + " TB";
                }
            }

            public int StatusCode { get { return (int)Info.Status; } }
            public string JobId { get { return Info.JobID.ToString(); } }
            public string TimeOfArrival { get { return Info.TimeOfArrival.ToString("dd. MM. yyyy HH:mm:ss"); } }
            public string HostName { get { return Info.HostName; } }
            public string UserName { get { return Info.UserName; } }
            public string DocumentName { get { return Info.DocumentName; } }
            public string TargetPrinterShortName { get { return Info.TargetPrinterID.HasValue ? Management.Printers[Info.TargetPrinterID.Value].ShortName : "???"; } }
            public string DataFileSize { get { return SizeString; } }
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

        public HttpListener(int listenPort)
        {
            Listener = new System.Net.HttpListener();
            Listener.Prefixes.Add("http://+:" + listenPort + "/");

            Template.FileSystem = new DotLiquid.FileSystems.LocalFileSystem(Path.Combine(Util.ProgramDirectory, "Templates"));

            TemplateCache = new Dictionary<string, Template>();
            using (var r = new StreamReader(Path.Combine(Util.ProgramDirectory, "Templates", "jobs.liquid")))
            {
                TemplateCache.Add("jobs", Template.Parse(r.ReadToEnd()));
            }
        }

        public void Start()
        {
            Logger.Debug("starting HttpListener");
            StopNow = false;
            Listener.Start();
            Listener.BeginGetContext(new AsyncCallback(ListenerCallback), Listener);
        }

        public void Stop()
        {
            Logger.Debug("stopping HttpListener");
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

        private string PrinterStatsString
        {
            get
            {
                var printerToJobCount = new Dictionary<uint, int>();
                foreach (var job in Management.Jobs.Values)
                {
                    if (job.TargetPrinterID.HasValue)
                    {
                        if (!printerToJobCount.ContainsKey(job.TargetPrinterID.Value))
                        {
                            printerToJobCount[job.TargetPrinterID.Value] = 0;
                        }
                        ++(printerToJobCount[job.TargetPrinterID.Value]);
                    }
                }
                var printers = Management.Printers;
                var printerJobCountStrings = new List<string>();
                foreach (var printerAndJobCount in printerToJobCount)
                {
                    var printerName = printers[printerAndJobCount.Key].ShortName;
                    printerJobCountStrings.Add(string.Format("{0}: {1}", printerName, printerAndJobCount.Value));
                }

                return string.Join(" \u00B7 ", printerJobCountStrings);
            }
        }

        private void HandleRequest(System.Net.HttpListenerContext context)
        {
            Logger.InfoFormat("{0} {1}", context.Request.HttpMethod, context.Request.Url.AbsolutePath);

            if (context.Request.HttpMethod == "GET")
            {
                if (context.Request.Url.AbsolutePath == "/jobs")
                {
                    var variables = new Hash();
                    variables.Add("jobs", Management.Jobs.Values.OrderBy((k) => k.TimeOfArrival).Reverse().Select((j) => new JobInfoDrop(j)).ToArray());
                    variables.Add("printers", Management.Printers.Values.OrderBy((k) => k.PrinterID).Select((p) => new PrinterInfoDrop(p)).ToArray());
                    variables.Add("printer_statistics", PrinterStatsString);
                    variables.Add("delete_times", Config.DeletionAgeMinutesOptions);
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
                            using (var s = new FileStream(Path.Combine(Util.ProgramDirectory, "Static", path), FileMode.Open, FileAccess.Read))
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

                    Logger.InfoFormat("dojob parameters: {0}", parameterString);

                    // split them up
                    var parameters = Util.DecodeUriParameters(parameterString);

                    if (!parameters.ContainsKey("do") || !parameters.ContainsKey("jobID"))
                    {
                        Logger.Debug("dojob without 'do' or 'jobID' parameter");
                        Send400MissingParameter(context.Response);
                        return;
                    }

                    ulong jobID;
                    if (!ulong.TryParse(parameters["jobID"], out jobID))
                    {
                        Logger.Debug("dojob 'jobID' parameter unparseable");
                        Send400MalformedParameter(context.Response);
                        return;
                    }

                    var jobs = Management.Jobs;
                    if (!jobs.ContainsKey(jobID))
                    {
                        Logger.Debug("dojob with unknown jobID");
                        Send400ParameterResourceNonexistent(context.Response);
                        return;
                    }

                    var doParam = parameters["do"];
                    if (doParam == "sendJobToPrinter")
                    {
                        if (!parameters.ContainsKey("printerID"))
                        {
                            Logger.Debug("sendJobToPrinter without 'printerID' parameter");
                            Send400MissingParameter(context.Response);
                            return;
                        }

                        uint printerID;
                        if (!uint.TryParse(parameters["printerID"], out printerID))
                        {
                            Logger.Debug("sendJobToPrinter 'printerID' parameter unparseable");
                            Send400MalformedParameter(context.Response);
                            return;
                        }

                        var printers = Management.Printers;
                        if (!printers.ContainsKey(printerID))
                        {
                            Logger.Debug("sendJobToPrinter with unknown printerID");
                            Send400ParameterResourceNonexistent(context.Response);
                            return;
                        }

                        // send!!
                        Logger.DebugFormat("sendJobToPrinter: sending job {0} to printer {1}", jobID, printerID);
                        printers[printerID].Sender.Send(jobs[jobID]);
                        jobs[jobID].Status = JobInfo.JobStatus.SentToPrinter;
                        jobs[jobID].TargetPrinterID = printerID;
                    }
                    else if (doParam == "removeJob")
                    {
                        Logger.DebugFormat("removeJob: removing job {0}", jobID);

                        // remove it
                        Management.RemoveJob(jobID);
                    }
                    else if (doParam == "resetJob")
                    {
                        if (Management.Jobs[jobID].Status == JobInfo.JobStatus.SentToPrinter)
                        {
                            Logger.DebugFormat("resetJob: resetting job {0}", jobID);

                            Management.Jobs[jobID].Status = JobInfo.JobStatus.ReadyToPrint;
                        }
                        else
                        {
                            Logger.DebugFormat("resetJob: not resetting job {0} (status {1})", jobID, Management.Jobs[jobID].Status);
                        }
                    }
                    else
                    {
                        Logger.DebugFormat("dojob: unknown 'do' value '{0}'", doParam);

                        Send404(context.Response);
                        return;
                    }

                    SendOkJson(context.Response);
                }
                else if (context.Request.Url.AbsolutePath == "/deletesentjobs")
                {
                    var parameterBody = BinaryStreamReader.ReadStreamToEnd(context.Request.InputStream);
                    var parameterString = Encoding.Default.GetString(parameterBody);
                    Logger.InfoFormat("deletesentjobs parameters: {0}", parameterString);
                    var parameters = Util.DecodeUriParameters(parameterString);

                    if (!parameters.ContainsKey("minutes"))
                    {
                        Logger.Debug("deletesentjobs without 'minutes' parameter");
                        Send400MissingParameter(context.Response);
                        return;
                    }

                    uint minutes;
                    if (!uint.TryParse(parameters["minutes"], out minutes))
                    {
                        Logger.Debug("deletesentjobs 'minutes' parameter unparseable");
                        Send400MalformedParameter(context.Response);
                        return;
                    }

                    Logger.DebugFormat("deletesentjobs: deleting jobs older than {0} minutes", minutes);

                    // a'ight
                    var jobs = Management.Jobs.Values;
                    foreach (var job in jobs)
                    {
                        if (job.Status != JobInfo.JobStatus.SentToPrinter)
                        {
                            // not sent to printer
                            continue;
                        }
                        if ((DateTime.Now - job.TimeOfArrival).TotalMinutes <= minutes)
                        {
                            // too young
                            continue;
                        }

                        Management.RemoveJob(job.JobID);
                    }

                    SendOkJson(context.Response);
                }
                else
                {
                    Send404(context.Response);
                    return;
                }
            }
            else
            {
                Send404(context.Response);
                return;
            }
        }

        private void ListenerCallback(IAsyncResult result)
        {
            // fetch the context
            var context = Listener.EndGetContext(result);

            // get ready for the next request
            Listener.BeginGetContext(new AsyncCallback(ListenerCallback), Listener);

            try
            {
                HandleRequest(context);
            }
            catch (Exception exc)
            {
                Logger.Error("exception thrown while handling HTTP connection", exc);
            }
            finally
            {
                context.Response.Close();
            }
        }
    }
}
