using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DistributePrintJobs
{
    static class Management
    {
        private static Dictionary<ulong, JobInfo> JobDictionary = new Dictionary<ulong, JobInfo>();
        private static Dictionary<uint, PrinterInfo> PrinterDictionary = new Dictionary<uint, PrinterInfo>();
        private static Object ManagementLock = new Object();

        public static Dictionary<ulong, JobInfo> Jobs
        {
            get
            {
                lock (ManagementLock)
                {
                    return new Dictionary<ulong, JobInfo>(JobDictionary);
                }
            }
        }

        public static Dictionary<uint, PrinterInfo> Printers
        {
            get
            {
                lock (ManagementLock)
                {
                    return new Dictionary<uint, PrinterInfo>(PrinterDictionary);
                }
            }
        }

        public static void AddPrinter(PrinterInfo info)
        {
            lock (ManagementLock)
            {
                PrinterDictionary[info.PrinterID] = info;
            }
        }

        public static void AddJob(JobInfo info)
        {
            lock (ManagementLock)
            {
                JobDictionary[info.JobID] = info;
            }
        }
    }
}
