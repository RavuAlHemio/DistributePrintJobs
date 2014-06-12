// Released into the public domain.
// http://creativecommons.org/publicdomain/zero/1.0/

using System;

namespace DistributePrintJobs
{
    public class PrinterInfo
    {
        private static uint NextPrinterID = 0;
        private static Object NextPrinterIDLock = new Object();

        public PrinterInfo()
        {
            lock (NextPrinterIDLock)
            {
                PrinterID = NextPrinterID;
                ++NextPrinterID;
            }
        }

        /// <summary>
        /// The ID of this printer.
        /// </summary>
        public uint PrinterID { get; private set; }

        /// <summary>
        /// A string describing the printer in short.
        /// </summary>
        public string ShortName { get; set; }

        /// <summary>
        /// The sender responsible for sending print jobs to this printer.
        /// </summary>
        public ISender Sender { get; set; }
    }
}
