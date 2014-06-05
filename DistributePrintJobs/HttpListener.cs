using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DotLiquid;

namespace DistributePrintJobs
{
    class HttpListener
    {
        private System.Net.HttpListener Listener { get; set; }
        private bool StopNow { get; set; }
        private Dictionary<string, Template> TemplateCache { get; set; }

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
            public string TargetPrinterId { get { return Info.TargetPrinterID.HasValue ? Info.TargetPrinterID.Value.ToString() : "-1"; } }
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

        private void SendError(System.Net.HttpListenerResponse response, int code, string description, string body)
        {
            response.StatusCode = code;
            response.StatusDescription = description;
            response.ContentType = "text/plain; charset=utf-8";

            var textBytes = Encoding.UTF8.GetBytes(body);
            response.ContentLength64 = textBytes.LongLength;
            response.OutputStream.Write(textBytes, 0, textBytes.Length);
            response.OutputStream.Close();
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
                        using (var s = new FileStream(Path.Combine("Static", path), FileMode.Open))
                        {
                            SendOk(context.Response, "application/octet-stream", BinaryStreamReader.ReadStreamToEnd(s));
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

                    if (!parameters.ContainsKey("do"))
                    {
                        Send404(context.Response);
                        return;
                    }

                    var doParam = parameters["do"];
                    if (doParam == "sendJobToPrinter")
                    {
                        // TODO
                    }
                    else if (doParam == "removeJob")
                    {
                        // TODO
                    }
                    else if (doParam == "resetJob")
                    {
                        // TODO
                    }
                    else
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
    }
}
