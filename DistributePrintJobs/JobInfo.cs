using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DistributePrintJobs
{
    class JobInfo
    {
        /// <summary>
        /// The hostname of the computer which printed this document.
        /// </summary>
        public string HostName { get; set; }

        /// <summary>
        /// The username of the user who printed this document.
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// The filename or title of the document being printed.
        /// </summary>
        public string DocumentName { get; set; }

        private byte[] DataBytes;

        /// <summary>
        /// The bytes comprising the document.
        /// </summary>
        public byte[] Data
        {
            get
            {
                return DataBytes.Clone() as byte[];
            }

            set
            {
                DataBytes = value.Clone() as byte[];
            }
        }

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
