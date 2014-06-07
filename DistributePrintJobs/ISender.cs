using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DistributePrintJobs
{
    /// <summary>
    /// A sender is responsible for sending print jobs to a printer.
    /// </summary>
    public interface ISender
    {
        /// <summary>
        /// Send a job to the printer.
        /// </summary>
        /// <param name="job">The job to send to the printer.</param>
        void Send(JobInfo job);
    }
}
