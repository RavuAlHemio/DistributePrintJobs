// Released into the public domain.
// http://creativecommons.org/publicdomain/zero/1.0/

using System.ServiceProcess;
using DistributePrintJobs;

namespace DistributePrintJobsService
{
    public partial class DistributePrintJobsService : ServiceBase
    {
        private LpdListener _lpdListener;
        private HttpListener _httpListener;

        public DistributePrintJobsService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            Util.SetupLogging();

            Config.LoadConfig();
            Management.ReadJobs();

            _lpdListener = new LpdListener();
            _lpdListener.NewJobReceived += (sender, newJobInfo) => Management.AddJob(newJobInfo);
            _lpdListener.Start();

            _httpListener = new DistributePrintJobs.HttpListener(Config.HttpListenPort);
            _httpListener.Start();
        }

        protected override void OnStop()
        {
            _lpdListener.Stop();
            _httpListener.Stop();
        }
    }
}
