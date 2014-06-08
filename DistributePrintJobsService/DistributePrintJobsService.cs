using System;
using System.ServiceProcess;
using DistributePrintJobs;

namespace DistributePrintJobsService
{
    public partial class DistributePrintJobsService : ServiceBase
    {
        private LpdListener TheLpdListener;
        private HttpListener TheHttpListener;

        public DistributePrintJobsService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            Util.SetupLogging();

            Config.LoadConfig();
            Management.ReadJobs();

            TheLpdListener = new LpdListener();
            TheLpdListener.NewJobReceived += (sender, newJobInfo) =>
            {
                Management.AddJob(newJobInfo);
            };
            TheLpdListener.Start();

            TheHttpListener = new DistributePrintJobs.HttpListener(Config.HttpListenPort);
            TheHttpListener.Start();
        }

        protected override void OnStop()
        {
            TheLpdListener.Stop();
            TheHttpListener.Stop();
        }
    }
}
