using System;
using System.ServiceProcess;

namespace DistributePrintJobsService
{
    public partial class DistributePrintJobsService : ServiceBase
    {
        public DistributePrintJobsService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
        }

        protected override void OnStop()
        {
        }
    }
}
