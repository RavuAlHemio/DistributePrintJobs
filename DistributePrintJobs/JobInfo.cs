// Released into the public domain.
// http://creativecommons.org/publicdomain/zero/1.0/

using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace DistributePrintJobs
{
    [JsonObject(MemberSerialization.OptIn)]
    public class JobInfo
    {
        /// <summary>
        /// Possible print job statuses.
        /// </summary>
        public enum JobStatus
        {
            /// <summary>
            /// The job status is unknown.
            /// </summary>
            Unknown = 0,

            /// <summary>
            /// The job is ready to print.
            /// </summary>
            ReadyToPrint = 1,

            /// <summary>
            /// The job has been sent to the printer.
            /// </summary>
            SentToPrinter = 2
        }

        private static ulong NextJobID = 0;
        private static Object NextJobIDLock = new Object();

        public JobInfo()
        {
            lock (NextJobIDLock)
            {
                JobID = NextJobID;
                ++NextJobID;
            }
            Status = JobStatus.Unknown;
            TargetPrinterID = null;
        }

        [OnDeserialized]
        internal void JsonDeserialized(StreamingContext context)
        {
            // assign new job ID without causing a conflict
            lock (NextJobIDLock)
            {
                if (JobID >= NextJobID)
                {
                    NextJobID = JobID + 1;
                }
            }
        }

        /// <summary>
        /// The ID of this job.
        /// </summary>
        [JsonProperty]
        public ulong JobID { get; private set; }

        /// <summary>
        /// What status the job is currently in.
        /// </summary>
        [JsonProperty]
        public JobStatus Status { get; set; }

        /// <summary>
        /// The date/time when this print job arrived.
        /// </summary>
        [JsonProperty]
        public DateTime TimeOfArrival { get; set; }

        /// <summary>
        /// The hostname of the computer which printed this document.
        /// </summary>
        [JsonProperty]
        public string HostName { get; set; }

        /// <summary>
        /// The username of the user who printed this document.
        /// </summary>
        [JsonProperty]
        public string UserName { get; set; }

        /// <summary>
        /// The filename or title of the document being printed.
        /// </summary>
        [JsonProperty]
        public string DocumentName { get; set; }

        /// <summary>
        /// The path to the document data file.
        /// </summary>
        [JsonProperty]
        public string DataFilePath { get; set; }

        /// <summary>
        /// The size of the data file.
        /// </summary>
        [JsonProperty]
        public long DataFileSize { get; set; }

        /// <summary>
        /// The ID of the printer to which this job has been sent.
        /// </summary>
        [JsonProperty]
        public uint? TargetPrinterID { get; set; }

        /*
        HOFFICE201
        Phosek
        JUntitled - Notepad
        ldfA040OFFICE201
        UdfA040OFFICE201
        NUntitled - Notepad
        */
    }
}
