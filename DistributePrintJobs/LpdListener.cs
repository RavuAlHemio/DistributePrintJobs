using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace DistributePrintJobs
{
    /// <summary>
    /// Listens to and processes incoming Line Printer Daemon requests.
    /// </summary>
    /// <remarks>See RFC1179 for a documentation of the protocol.</remarks>
    class LpdListener
    {
        public static JobInfo ParseJobInfo(string jobInfoString, Dictionary<string, byte[]> dataFiles)
        {
            var ret = new JobInfo();

            foreach (var line in jobInfoString.Split('\n'))
            {
                if (line.Length < 2)
                {
                    continue;
                }

                var letter = line[0];
                var param = line.Substring(1);
                switch (letter)
                {
                    case (char)ControlFileCharacters.HostName:
                        ret.HostName = param;
                        break;
                    case (char)ControlFileCharacters.UserIdentification:
                        ret.UserName = param;
                        break;
                    case (char)ControlFileCharacters.JobName:
                        ret.DocumentName = param;
                        break;
                    case (char)ControlFileCharacters.NameOfSourceFile:
                        ret.DocumentName = param;
                        break;
                    case (char)ControlFileCharacters.PrintFileWithControlCharacters:
                        ret.Data = dataFiles[param];
                        break;
                    case (char)ControlFileCharacters.UnlinkDataFile:
                        dataFiles.Remove(param);
                        break;
                    case (char)ControlFileCharacters.PlotCifFile:
                    case (char)ControlFileCharacters.PrintDviFile:
                    case (char)ControlFileCharacters.PrintFormattedFile:
                    case (char)ControlFileCharacters.PlotBerkeleyFile:
                    case (char)ControlFileCharacters.PrintDitroffOutput:
                    case (char)ControlFileCharacters.PrintPostscript:
                    case (char)ControlFileCharacters.PrintFortranCarriageControlFile:
                    case (char)ControlFileCharacters.PrintTroffOutput:
                    case (char)ControlFileCharacters.PrintRasterFile:
                        throw new FormatException("cannot print this format");
                    default:
                        // ignore it...
                        break;
                }
            }

            return ret;
        }

        public delegate void NewJobEventHandler(object sender, JobInfo newJobInfo);

        public event NewJobEventHandler NewJobReceived;

        enum CommandCode
        {
            /// <summary>
            /// Start printing any jobs that haven't been printed yet.
            /// </summary>
            PrintAnyWaitingJobs = 0x01,

            /// <summary>
            /// The client will now send information about a new printing job. This information is
            /// governed by a secondary set of commands. <see cref="JobCommandCode"/>
            /// </summary>
            ReceiveAPrinterJob = 0x02,

            /// <summary>
            /// The server replies with a short listing of current print jobs.
            /// </summary>
            SendQueueStateShort = 0x03,

            /// <summary>
            /// The server replies with a long listing of current print jobs.
            /// </summary>
            SendQueueStateLong = 0x04,

            /// <summary>
            /// Removes print jobs from a queue.
            /// </summary>
            RemoveJobs = 0x05
        }

        enum JobCommandCode
        {
            /// <summary>
            /// The client has changed its mind and doesn't want the current job printed after all.
            /// </summary>
            AbortJob = 0x01,

            /// <summary>
            /// The client wishes to send metadata about the print job.
            /// </summary>
            ReceiveControlFile = 0x02,

            /// <summary>
            /// The client wishes to send the actual print job data.
            /// </summary>
            ReceiveDataFile = 0x03
        }

        enum ControlFileCharacters
        {
            /// <summary>
            /// The class to be printed on a banner page.
            /// </summary>
            JobClass = 'C',

            /// <summary>
            /// The host which sent the print job.
            /// </summary>
            /// <remarks>Required.</remarks>
            HostName = 'H',

            /// <summary>
            /// If printing a formatted file ('f'), specify the indent of the text.
            /// </summary>
            IndentPrinting = 'I',

            /// <summary>
            /// The name of the job for the banner page.
            /// </summary>
            JobName = 'J',

            /// <summary>
            /// Causes the banner page to be printed. May contain a user name.
            /// </summary>
            PrintBannerPage = 'L',

            /// <summary>
            /// The user should be notified when printing is done.
            /// </summary>
            MailWhenPrinted = 'M',

            /// <summary>
            /// The name of the file that has been sent to the printer.
            /// </summary>
            NameOfSourceFile = 'N',

            /// <summary>
            /// Specifies which user triggered the print job.
            /// </summary>
            UserIdentification = 'P',

            /// <summary>
            /// Specifies symbolic link data. Ignored if the data file is not linked.
            /// </summary>
            SymbolicLinkData = 'S',

            /// <summary>
            /// Title for printing using the 'p' command.
            /// </summary>
            TitleForP = 'T',

            /// <summary>
            /// Removes the data file.
            /// </summary>
            UnlinkDataFile = 'U',

            /// <summary>
            /// The width of the output.
            /// </summary>
            WidthOfOutput = 'W',

            /// <summary>
            /// Specifies the Roman font for Troff printing.
            /// </summary>
            TroffRomanFont = '1',

            /// <summary>
            /// Specifies the Italic font for Troff printing.
            /// </summary>
            TroffItalicFont = '2',

            /// <summary>
            /// Specifies the Bold font for Troff printing.
            /// </summary>
            TroffBoldFont = '3',

            /// <summary>
            /// Specifies the Special font for Troff printing.
            /// </summary>
            TroffSpecialFont = '4',

            /// <summary>
            /// Plots a CIF (Caltech Intermediate Form) file.
            /// </summary>
            PlotCifFile = 'c',

            /// <summary>
            /// Prints a DVI (DeVice Independent; TeX output) file.
            /// </summary>
            PrintDviFile = 'd',

            /// <summary>
            /// Formats and prints a plain-text file, stripping special characters.
            /// </summary>
            PrintFormattedFile = 'f',

            /// <summary>
            /// Plots a file in Berkeley Unix plot library output format.
            /// </summary>
            PlotBerkeleyFile = 'g',

            /// <summary>
            /// Reserved for Kerberized LPR clients and servers.
            /// </summary>
            Kerberos = 'k',

            /// <summary>
            /// Formats and prints a plain-text file, not stripping special characters.
            /// </summary>
            PrintFileWithControlCharacters = 'l',

            /// <summary>
            /// Prints a file in ditroff output format.
            /// </summary>
            PrintDitroffOutput = 'n',

            /// <summary>
            /// Prints PostScript output.
            /// </summary>
            PrintPostscript = 'o',

            /// <summary>
            /// Prints a data file with heading, page numbers and pagination.
            /// </summary>
            PrintPrFile = 'p',

            /// <summary>
            /// Prints a file with Fortran carraige control commands.
            /// </summary>
            PrintFortranCarriageControlFile = 'r',

            /// <summary>
            /// Prints a file in Troff output format.
            /// </summary>
            PrintTroffOutput = 't',

            /// <summary>
            /// Prints a file in Sun raster format.
            /// </summary>
            PrintRasterFile = 'v',

            /// <summary>
            /// Reserved for use with the Palladium print system.
            /// </summary>
            Palladium = 'z'
        }

        private static readonly Regex ControlFileNamePattern = new Regex("^cfA([0-9]{3})(.+)$");
        private static readonly Regex DataFileNamePattern = new Regex("^dfA([0-9]{3})(.+)$");
        private static readonly Regex WhiteSpacePattern = new Regex("[ \t\x0B\x0C]+");

        protected virtual void OnNewJobReceived(JobInfo job)
        {
            if (NewJobReceived != null)
            {
                NewJobReceived(this, job);
            }
        }

        private byte[] ReadUntilByte(byte thisByte, NetworkStream stream)
        {
            var bytes = new List<byte>();
            for (; ; )
            {
                var b = stream.ReadByte();

                if (b == -1)
                {
                    // it's over!
                    throw new EndOfStreamException();
                }

                bytes.Add((byte)b);

                if (b == thisByte)
                {
                    return bytes.ToArray();
                }
            }
        }

        private byte[] ReadBytes(int count, NetworkStream stream)
        {
            var ret = new byte[count];
            int offset = 0;

            while (offset < count)
            {
                int bytesRead = stream.Read(ret, offset, count - offset);

                if (bytesRead == 0)
                {
                    throw new EndOfStreamException();
                }

                offset += bytesRead;
            }

            return ret;
        }

        private byte[] ReadUntilEnd(NetworkStream stream)
        {
            var ret = new List<byte>();
            var buf = new byte[1024];

            for (; ; )
            {
                int bytesRead = stream.Read(buf, 0, buf.Length);
                
                if (bytesRead == 0)
                {
                    return ret.ToArray();
                }

                ret.AddRange(buf.Take(bytesRead));
            }
        }

        private byte[] ReadUntilLineFeed(NetworkStream stream)
        {
            return ReadUntilByte(0x0A, stream);
        }

        private byte[] ReadUntilNulByte(NetworkStream stream)
        {
            return ReadUntilByte(0x00, stream);
        }

        private void ReturnSuccess(NetworkStream stream)
        {
            stream.WriteByte(0x00);
        }

        private void ReturnInvalidSyntax(NetworkStream stream)
        {
            stream.WriteByte(0x01);
        }

        private void ReturnUnsupportedPrintType(NetworkStream stream)
        {
            stream.WriteByte(0x02);
        }

        private string GetSingleStringArgument(byte[] command)
        {
            var stringArgument = Encoding.Default.GetString(command, 1, command.Length - 1);
            if (stringArgument.EndsWith("\n"))
            {
                stringArgument = stringArgument.Substring(0, stringArgument.Length - 1);
            }
            return stringArgument;
        }

        private void HandleConnection(NetworkStream stream)
        {
            for (; ; )
            {
                byte[] command;

                try
                {
                    command = ReadUntilLineFeed(stream);
                }
                catch (EndOfStreamException)
                {
                    return;
                }

                switch (command[0])
                {
                    case (byte)CommandCode.PrintAnyWaitingJobs:
                    {
                        var queueName = GetSingleStringArgument(command);

                        // FIXME: check if this queue exists

                        // don't actually do anything :X

                        ReturnSuccess(stream);

                        break;
                    }

                    case (byte)CommandCode.ReceiveAPrinterJob:
                    {
                        var queueName = GetSingleStringArgument(command);

                        // FIXME: check if this queue exists

                        ReturnSuccess(stream);

                        // switch to submission mode
                        HandleJobSubmission(stream);

                        // nothing may follow
                        return;
                    }
                }
            }
        }

        private void HandleJobSubmission(NetworkStream stream)
        {
            var jobFiles = new Dictionary<string, byte[]>();
            string controlFileName = null;

            for (; ; )
            {
                byte[] command;
                try
                {
                    command = ReadUntilLineFeed(stream);
                }
                catch (EndOfStreamException)
                {
                    // it's over!
                    break;
                }

                switch (command[0])
                {
                    case (byte)JobCommandCode.AbortJob:
                    {
                        ReturnSuccess(stream);

                        break;
                    }

                    case (byte)JobCommandCode.ReceiveControlFile:
                    {
                        var controlFileDataString = GetSingleStringArgument(command);
                        var controlFileData = WhiteSpacePattern.Split(controlFileDataString, 2);
                        if (controlFileData.Length != 2)
                        {
                            ReturnInvalidSyntax(stream);
                            throw new InvalidDataException("invalid syntax");
                        }

                        var length = int.Parse(controlFileData[0]);
                        controlFileName = controlFileData[1];

                        // parse the name
                        var controlFileNameMatch = ControlFileNamePattern.Match(controlFileName);

                        if (!controlFileNameMatch.Success)
                        {
                            ReturnInvalidSyntax(stream);
                            throw new InvalidDataException("control file name doesn't match expected pattern");
                        }

                        // signal that we parsed the command successfully
                        ReturnSuccess(stream);

                        var controlBytes = ReadBytes(length, stream);

                        // store the control file
                        jobFiles[controlFileName] = controlBytes;

                        // read the last byte
                        var b = stream.ReadByte();
                        if (b == 0)
                        {
                            // "thanks!"
                            ReturnSuccess(stream);
                        }
                        else
                        {
                            ReturnInvalidSyntax(stream);
                            throw new InvalidDataException("control file not succeeded by NUL");
                        }

                        break;
                    }

                    case (byte)JobCommandCode.ReceiveDataFile:
                    {
                        var dataFileDataString = GetSingleStringArgument(command);
                        var dataFileData = WhiteSpacePattern.Split(dataFileDataString, 2);
                        if (dataFileData.Length != 2)
                        {
                            ReturnInvalidSyntax(stream);
                            throw new InvalidDataException("invalid syntax");
                        }

                        var length = int.Parse(dataFileData[0]);
                        var dataFileName = dataFileData[1];

                        // parse the name
                        var dataFileNameMatch = DataFileNamePattern.Match(dataFileName);

                        if (!dataFileNameMatch.Success)
                        {
                            ReturnInvalidSyntax(stream);
                            throw new InvalidDataException("data file name doesn't match expected pattern");
                        }

                        // must send this so that the client responds
                        ReturnSuccess(stream);

                        if (length == 0)
                        {
                            var dataBytes = ReadUntilEnd(stream);

                            jobFiles[dataFileName] = dataBytes;

                            // "thanks!"
                            ReturnSuccess(stream);
                        }
                        else
                        {
                            var dataBytes = ReadBytes(length, stream);

                            jobFiles[dataFileName] = dataBytes;

                            // read the last byte
                            var b = stream.ReadByte();
                            if (b == 0)
                            {
                                // "thanks!"
                                ReturnSuccess(stream);
                            }
                            else
                            {
                                ReturnInvalidSyntax(stream);
                                throw new InvalidDataException("data file not succeeded by NUL");
                            }
                        }
                        break;
                    }
                }
            }

            // decode the control file
            var controlString = Encoding.Default.GetString(jobFiles["controlFileName"]);

            // parse it, with all the magic
            JobInfo jobInfo;
            try
            {
                jobInfo = ParseJobInfo(controlString, jobFiles);
            }
            catch (FormatException)
            {
                ReturnUnsupportedPrintType(stream);

                // FIXME: log this

                return;
            }

            OnNewJobReceived(jobInfo);
        }

        internal void ListenProc()
        {
            var lpdListener = new TcpListener(IPAddress.Any, 515);
            lpdListener.Start();
            for (; ; )
            {
                var client = lpdListener.AcceptTcpClient();
                var stream = client.GetStream();
                ThreadPool.QueueUserWorkItem((state) =>
                {
                    try { HandleConnection(stream); }
                    catch (Exception e) { Console.Error.WriteLine(e.ToString()); }
                });
            }
        }
    }
}
