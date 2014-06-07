using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace DistributePrintJobs
{
    public static class Management
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

        public static void RemovePrinter(uint printerID)
        {
            lock (ManagementLock)
            {
                PrinterDictionary.Remove(printerID);
            }
        }

        public static void AddJob(JobInfo info)
        {
            lock (ManagementLock)
            {
                JobDictionary[info.JobID] = info;

                WriteJobs();
            }
        }

        public static void RemoveJob(ulong jobID)
        {
            JobInfo job;

            // remove the job (under the lock)
            lock (ManagementLock)
            {
                job = JobDictionary[jobID];
                JobDictionary.Remove(jobID);

                WriteJobs();
            }

            // delete the data file
            if (job.DataFilePath != null)
            {
                File.Delete(job.DataFilePath);
            }
        }

        private static void WriteJobs()
        {
            using (var w = new StreamWriter(Path.Combine("Jobs", "Jobs.json"), false, Encoding.UTF8))
            {
                w.Write(JsonConvert.SerializeObject(JobDictionary));
            }
        }

        public static void ReadJobs()
        {
            try
            {
                using (var r = new StreamReader(Path.Combine("Jobs", "Jobs.json"), Encoding.UTF8))
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
