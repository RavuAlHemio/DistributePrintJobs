// Released into the public domain.
// http://creativecommons.org/publicdomain/zero/1.0/

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using log4net;
using Newtonsoft.Json;

namespace DistributePrintJobs
{
    public static class Management
    {
        private static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
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
            Logger.DebugFormat("adding printer {0} ({1})", info.PrinterID, info.ShortName);
            lock (ManagementLock)
            {
                PrinterDictionary[info.PrinterID] = info;
            }
        }

        public static void RemovePrinter(uint printerID)
        {
            Logger.DebugFormat("removing printer {0}", printerID);
            lock (ManagementLock)
            {
                PrinterDictionary.Remove(printerID);
            }
        }

        public static void AddJob(JobInfo info)
        {
            Logger.DebugFormat("adding print job {0}", info.JobID);
            lock (ManagementLock)
            {
                JobDictionary[info.JobID] = info;

                WriteJobs();
            }
        }

        public static void RemoveJob(ulong jobID)
        {
            JobInfo job;

            Logger.DebugFormat("removing print job {0}", jobID);

            // remove the job (under the lock)
            lock (ManagementLock)
            {
                job = JobDictionary[jobID];
                JobDictionary.Remove(jobID);

                WriteJobs();
            }

            if (job.DataFilePath != null)
            {
                Logger.DebugFormat("deleting print job data file {0}", job.DataFilePath);
                File.Delete(job.DataFilePath);
            }
        }

        private static void WriteJobs()
        {
            Logger.DebugFormat("writing out job list");
            using (var w = new StreamWriter(Path.Combine(Config.JobDirectory, "Jobs.json"), false, Encoding.UTF8))
            {
                w.Write(JsonConvert.SerializeObject(JobDictionary));
            }
        }

        public static void ReadJobs()
        {
            Logger.DebugFormat("reading in job list");
            try
            {
                using (var r = new StreamReader(Path.Combine(Config.JobDirectory, "Jobs.json"), Encoding.UTF8))
                {
                    JobDictionary = JsonConvert.DeserializeObject<Dictionary<ulong, JobInfo>>(r.ReadToEnd());
                }
            }
            catch (FileNotFoundException)
            {
                // never mind
            }
        }
    }
}
