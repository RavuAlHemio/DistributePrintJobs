// Released into the public domain.
// http://creativecommons.org/publicdomain/zero/1.0/

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
