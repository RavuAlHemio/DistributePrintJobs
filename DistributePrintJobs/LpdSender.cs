using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace DistributePrintJobs
{
    /// <summary>
    /// Sends print requests via the Line Printer Daemon protocol.
    /// </summary>
    /// <remarks>See RFC1179 for a documentation of the protocol.</remarks>
    class LpdSender : ISender
    {
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

        public IPAddress Address { get; set; }
        public string QueueName { get; set; }
        private int JobCounter { get; set; }

        private int IncrementJobCounter()
        {
            lock (this)
            {
                var thisJob = JobCounter;
                ++JobCounter;
                return thisJob;
            }
        }

        public void Send(JobInfo job)
        {
            // connect to server
            var client = new TcpClient();
            client.Connect(Address, 515);
            var stream = client.GetStream();

            var message = new List<byte>();
            int b;

            int thisJobNumber = IncrementJobCounter();
            var dataFileName = string.Format("dfA{0:03}{1}\n", thisJobNumber, job.HostName);
            var controlFileName = string.Format("cfA{0:03}{1}\n", thisJobNumber, job.HostName);
            
            // initiate the print request
            message.Add(0x02);
            message.AddRange(Encoding.ASCII.GetBytes(QueueName));
            message.Add(0x0A);
            stream.Write(message.ToArray(), 0, message.Count);
            message.Clear();

            // read ACK
            b = stream.ReadByte();
            if (b != 0x00)
            {
                throw new BadResponseException("creating a new print job", b);
            }

            // prepare the control file
            var controlFile = new StringBuilder();
            controlFile.AppendFormat("H{0}\n", job.HostName);
            controlFile.AppendFormat("P{0}\n", job.UserName);
            controlFile.AppendFormat("J{0}\n", job.DocumentName);
            controlFile.AppendFormat("l{0}\n", dataFileName);
            controlFile.AppendFormat("U{0}\n", dataFileName);
            controlFile.AppendFormat("N{0}\n", job.DocumentName);
            var controlFileBytes = Encoding.Default.GetBytes(controlFile.ToString());

            // send the control file metadata
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
                throw new BadResponseException("preparing to send control data", b);
            }

            // send the control file
            stream.Write(controlFileBytes, 0, controlFileBytes.Length);
            stream.WriteByte(0x00);

            // read ACK
            b = stream.ReadByte();
            if (b != 0x00)
            {
                throw new BadResponseException("sending control data", b);
            }

            // send the data file metadata
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
                throw new BadResponseException("preparing to send data", b);
            }

            using (var inStream = new FileStream(job.DataFilePath, FileMode.Open))
            {
                Util.CopyStream(inStream, stream, job.DataFileSize);
            }

            // send the data
            stream.WriteByte(0x00);

            // read ACK
            b = stream.ReadByte();
            if (b != 0x00)
            {
                throw new BadResponseException("sending data", b);
            }

            // close
            client.Close();
        }
    }
}
