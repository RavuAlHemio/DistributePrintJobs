using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using log4net;

namespace DistributePrintJobs
{
    /// <summary>
    /// Sends print requests via the Line Printer Daemon protocol.
    /// </summary>
    /// <remarks>See RFC1179 for a documentation of the protocol.</remarks>
    class LpdSender : ISender
    {
        private static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        class BadResponseException : Exception
        {
            public string Stage { get; protected set; }
            public int Response { get; protected set; }

            public BadResponseException(string stage, int response)
                : base(string.Format("unexpected response while {0}: {1}", stage, response))
            {
                Stage = stage;
                Response = response;
            }
        }

        public LpdSender()
        {
            JobCounter = 0;
        }

        public string Host { get; set; }
        public string QueueName { get; set; }
        private int JobCounter { get; set; }

        private int IncrementJobCounter()
        {
            lock (this)
            {
                var thisJob = JobCounter;
                ++JobCounter;
                if (JobCounter > 999)
                {
                    JobCounter = 0;
                }
                return thisJob;
            }
        }

        public void Send(JobInfo job)
        {
            Logger.InfoFormat("sending job {0} to {1}", job.JobID, Host);

            Logger.DebugFormat("connecting to {0}", Host);
            var client = new TcpClient();
            client.Connect(Host, 515);
            var stream = client.GetStream();

            var message = new List<byte>();
            int b;

            int thisJobNumber = IncrementJobCounter();
            var dataFileName = string.Format("dfA{0:D3}{1}", thisJobNumber, job.HostName);
            var controlFileName = string.Format("cfA{0:D3}{1}", thisJobNumber, job.HostName);
            Logger.DebugFormat("LpdSender job number is {0}; control file name is '{1}' and data file name is '{2}'", Host, controlFileName, dataFileName);
            
            Logger.Debug("initiating the print request");
            message.Add(0x02);
            message.AddRange(Encoding.ASCII.GetBytes(QueueName));
            message.Add(0x0A);
            stream.Write(message.ToArray(), 0, message.Count);
            message.Clear();

            // read ACK
            b = stream.ReadByte();
            if (b != 0x00)
            {
                Logger.WarnFormat("got 0x{0:X2} after initiating the print request", b);
                throw new BadResponseException("creating a new print job", b);
            }

            Logger.Debug("preparing the control file");
            var controlFile = new StringBuilder();
            controlFile.AppendFormat("H{0}\n", job.HostName);
            controlFile.AppendFormat("P{0}\n", job.UserName);
            controlFile.AppendFormat("J{0}\n", job.DocumentName);
            controlFile.AppendFormat("l{0}\n", dataFileName);
            controlFile.AppendFormat("U{0}\n", dataFileName);
            controlFile.AppendFormat("N{0}\n", job.DocumentName);
            var controlFileBytes = Encoding.Default.GetBytes(controlFile.ToString());

            // send the control file metadata
            Logger.Debug("sending the control file metadata");
            message.Add(0x02);
            message.AddRange(Encoding.ASCII.GetBytes(controlFileBytes.Length.ToString()));
            message.Add(0x20);
            message.AddRange(Encoding.ASCII.GetBytes(controlFileName));
            message.Add(0x0A);
            stream.Write(message.ToArray(), 0, message.Count);
            message.Clear();

            // read ACK
            b = stream.ReadByte();
            if (b != 0x00)
            {
                Logger.WarnFormat("got 0x{0:X2} after sending the control file metadata", b);
                throw new BadResponseException("preparing to send control data", b);
            }

            Logger.Debug("sending the control file data");
            stream.Write(controlFileBytes, 0, controlFileBytes.Length);
            stream.WriteByte(0x00);

            // read ACK
            b = stream.ReadByte();
            if (b != 0x00)
            {
                Logger.WarnFormat("got 0x{0:X2} after sending the control file data", b);
                throw new BadResponseException("sending control data", b);
            }

            Logger.Debug("sending the data file metadata");
            message.Add(0x03);
            message.AddRange(Encoding.ASCII.GetBytes(job.DataFileSize.ToString()));
            message.Add(0x20);
            message.AddRange(Encoding.ASCII.GetBytes(dataFileName));
            message.Add(0x0A);
            stream.Write(message.ToArray(), 0, message.Count);
            message.Clear();

            // read ACK
            b = stream.ReadByte();
            if (b != 0x00)
            {
                Logger.WarnFormat("got 0x{0:X2} after sending the data file metadata", b);
                throw new BadResponseException("preparing to send data", b);
            }

            Logger.Debug("sending the data file data");
            using (var inStream = new FileStream(job.DataFilePath, FileMode.Open, FileAccess.Read))
            {
                Util.CopyStream(inStream, stream, job.DataFileSize);
            }
            stream.WriteByte(0x00);

            // read ACK
            b = stream.ReadByte();
            if (b != 0x00)
            {
                Logger.WarnFormat("got 0x{0:X2} after sending the data file data", b);
                throw new BadResponseException("sending data", b);
            }

            // close
            Logger.Debug("job sent");
            client.Close();
        }
    }
}
