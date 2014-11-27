// Released into the public domain.
// http://creativecommons.org/publicdomain/zero/1.0/

using System.ComponentModel;

namespace DistributePrintJobsService
{
    [RunInstaller(true)]
    public partial class ProjectInstaller : System.Configuration.Install.Installer
    {
        public ProjectInstaller()
        {
            InitializeComponent();
        }
    }
}
