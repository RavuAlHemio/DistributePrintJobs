// Released into the public domain.
// http://creativecommons.org/publicdomain/zero/1.0/

using System;

namespace DistributePrintJobs
{
    public class PrinterInfo
    {
        private static uint NextPrinterID = 0;
        private static Object NextPrinterIDLock = new Object();

        private object JobCountLock = new Object();

        public PrinterInfo()
        {
            lock (NextPrinterIDLock)
            {
                PrinterID = NextPrinterID;
                ++NextPrinterID;
            }

            JobCount = 0;
            DistributionFactor = 1;
        }

        /// <summary>
        /// The ID of this printer.
        /// </summary>
        public uint PrinterID { get; private set; }

        /// <summary>
        /// How eager the distributor should be in giving this printer jobs during automatic
        /// balancing. For example, if one printer has the value 5 and the other has the value 1,
        /// the printer with value 5 will receive five times as many jobs as the printer with value
        /// 1.
        /// </summary>
        public uint DistributionFactor { get; set; }

        /// <summary>
        /// Counts the number of jobs sent to this printer.
        /// </summary>
        public ulong JobCount { get; private set; }

        /// <summary>
        /// A string describing the printer in short.
        /// </summary>
        public string ShortName { get; set; }

        /// <summary>
        /// The sender responsible for sending print jobs to this printer.
        /// </summary>
        public ISender Sender { get; set; }

        /// <summary>
        /// Increments the job count by 1.
        /// </summary>
        public void IncrementJobCount()
        {
            lock (JobCountLock)
            {
                ++JobCount;
            }
        }
    }
}
