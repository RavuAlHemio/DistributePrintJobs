// Released into the public domain.
// http://creativecommons.org/publicdomain/zero/1.0/

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using log4net;

namespace DistributePrintJobs
{
    /// <summary>
    /// Listens to and processes incoming Line Printer Daemon requests.
    /// </summary>
    /// <remarks>See RFC1179 for a documentation of the protocol.</remarks>
    public class LpdListener
    {
        private static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly Regex ControlFileNamePattern = new Regex("^cfA([0-9]{3})(.+)$");
        private static readonly Regex DataFileNamePattern = new Regex("^dfA([0-9]{3})(.+)$");
        private static readonly Regex WhiteSpacePattern = new Regex("[ \t\x0B\x0C]+");
        private static readonly Encoding Utf8Encoding = new UTF8Encoding(false, true);

        private TcpListener Listener;

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

        protected static JobInfo ParseJobInfo(JobInfo jobInfo, string jobInfoString, Dictionary<string, string> dataFilePaths, Dictionary<string, long> dataFileSizes)
        {
            jobInfo.Status = JobInfo.JobStatus.ReadyToPrint;
            jobInfo.TimeOfArrival = DateTimeOffset.UtcNow;

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
                        jobInfo.HostName = param;
                        break;
                    case (char)ControlFileCharacters.UserIdentification:
                        jobInfo.UserName = param;
                        break;
                    case (char)ControlFileCharacters.JobName:
                        jobInfo.DocumentName = param;
                        break;
                    case (char)ControlFileCharacters.NameOfSourceFile:
                        jobInfo.DocumentName = param;
                        break;
                    case (char)ControlFileCharacters.PrintFileWithControlCharacters:
                        jobInfo.DataFilePath = dataFilePaths[param];
                        jobInfo.DataFileSize = dataFileSizes[param];
                        break;
                    case (char)ControlFileCharacters.UnlinkDataFile:
                        dataFilePaths.Remove(param);
                        dataFileSizes.Remove(param);
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
                        Logger.WarnFormat("requested printing in unsupported format '{0}'", letter);
                        throw new FormatException("cannot print this format");
                    default:
                        // ignore it...
                        Logger.WarnFormat("ignoring unknown LPD command file command '{0}'", letter);
                        break;
                }
            }

            return jobInfo;
        }

        protected virtual void OnNewJobReceived(JobInfo job)
        {
            if (NewJobReceived != null)
            {
                NewJobReceived(this, job);
            }
        }

        private static byte[] ReadUntilByte(byte thisByte, NetworkStream stream)
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

        private static byte[] ReadBytes(int count, NetworkStream stream)
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

        private static byte[] ReadUntilEnd(NetworkStream stream)
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

        private static byte[] ReadUntilLineFeed(NetworkStream stream)
        {
            return ReadUntilByte(0x0A, stream);
        }

        private static byte[] ReadUntilNulByte(NetworkStream stream)
        {
            return ReadUntilByte(0x00, stream);
        }

        private static void ReturnSuccess(NetworkStream stream)
        {
            stream.WriteByte(0x00);
        }

        private static void ReturnInvalidSyntax(NetworkStream stream)
        {
            stream.WriteByte(0x01);
        }

        private static void ReturnUnsupportedPrintType(NetworkStream stream)
        {
            stream.WriteByte(0x02);
        }

        private static void ReturnInternalIOError(NetworkStream stream)
        {
            stream.WriteByte(0x03);
        }

        private static string GetSingleStringArgument(byte[] command)
        {
            string stringArgument;
            try
            {
                stringArgument = Utf8Encoding.GetString(command, 1, command.Length - 1);
            }
            catch (DecoderFallbackException)
            {
                stringArgument = Encoding.Default.GetString(command, 1, command.Length - 1);
            }
            if (stringArgument.EndsWith("\n"))
            {
                stringArgument = stringArgument.Substring(0, stringArgument.Length - 1);
            }
            return stringArgument;
        }

        private static string GetJobDataFilename(ulong jobID, string lpdDataFilename)
        {
            return string.Format("{0:X4}-{1}.dat", jobID, lpdDataFilename);
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

                        Logger.DebugFormat("Received: print all waiting jobs on '{0}'", queueName);

                        // FIXME: check if this queue exists

                        // don't actually do anything :X

                        ReturnSuccess(stream);

                        break;
                    }

                    case (byte)CommandCode.ReceiveAPrinterJob:
                    {
                        var queueName = GetSingleStringArgument(command);

                        Logger.DebugFormat("Received: print a job on '{0}'", queueName);

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
            var jobFileReferences = new Dictionary<string, string>();
            var jobFileLengths = new Dictionary<string, long>();
            string controlFileName = null;

            // create a new job, reserving a new job ID
            var jobInfo = new JobInfo();

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

                        Logger.Debug("Received: abort current job");

                        // nothing left to do here
                        return;
                    }

                    case (byte)JobCommandCode.ReceiveControlFile:
                    {
                        var controlFileDataString = GetSingleStringArgument(command);
                        var controlFileData = WhiteSpacePattern.Split(controlFileDataString, 2);
                        if (controlFileData.Length != 2)
                        {
                            Logger.WarnFormat("'receive control file' command '{0}' not in format 'length filename'", controlFileDataString);
                            ReturnInvalidSyntax(stream);
                            throw new InvalidDataException("invalid syntax");
                        }

                        var length = int.Parse(controlFileData[0]);
                        controlFileName = controlFileData[1];

                        Logger.DebugFormat("Received: receive {0} bytes of control file named {1}", length, controlFileName);

                        // parse the name
                        var controlFileNameMatch = ControlFileNamePattern.Match(controlFileName);

                        if (!controlFileNameMatch.Success)
                        {
                            Logger.WarnFormat("'receive control file' filename '{0}' has invalid format", controlFileName);
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
                            Logger.Warn("'receive control file' control file contents not succeeded by NUL");
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
                            Logger.WarnFormat("'receive data file' command '{0}' not in format 'length filename'", dataFileDataString);
                            ReturnInvalidSyntax(stream);
                            throw new InvalidDataException("invalid syntax");
                        }

                        var length = int.Parse(dataFileData[0]);
                        var lpdDataFileName = dataFileData[1];

                        Logger.DebugFormat("Received: receive {0} bytes of data file named {1}", length, lpdDataFileName);

                        // parse the name
                        var dataFileNameMatch = DataFileNamePattern.Match(lpdDataFileName);

                        if (!dataFileNameMatch.Success)
                        {
                            Logger.WarnFormat("'receive data file' filename '{0}' has invalid format", lpdDataFileName);
                            ReturnInvalidSyntax(stream);
                            throw new InvalidDataException("data file name doesn't match expected pattern");
                        }

                        // must send this so that the client responds
                        ReturnSuccess(stream);

                        var outFileName = Path.Combine(Config.JobDirectory, GetJobDataFilename(jobInfo.JobID, lpdDataFileName));
                        using (var outStream = new FileStream(outFileName, FileMode.CreateNew, FileAccess.Write))
                        {
                            long? copyLength = (length == 0) ? null : (long?)length;

                            long actualLength = Util.CopyStream(stream, outStream, copyLength);

                            jobFileReferences[lpdDataFileName] = outFileName;
                            jobFileLengths[lpdDataFileName] = actualLength;
                        }

                        if (length == 0)
                        {
                            // "thanks!"
                            ReturnSuccess(stream);
                        }
                        else
                        {
                            // read the last byte
                            var b = stream.ReadByte();
                            if (b == 0)
                            {
                                // "thanks!"
                                ReturnSuccess(stream);
                            }
                            else
                            {
                                Logger.Warn("'receive data file' data file contents not succeeded by NUL");
                                ReturnInvalidSyntax(stream);
                                throw new InvalidDataException("data file not succeeded by NUL");
                            }
                        }
                        break;
                    }
                }
            }

            // decode the control file
            string controlString;
            try
            {
                controlString = Utf8Encoding.GetString(jobFiles[controlFileName]);
            }
            catch (DecoderFallbackException)
            {
                controlString = Encoding.Default.GetString(jobFiles[controlFileName]);
            }

            // parse it, with all the magic
            try
            {
                jobInfo = ParseJobInfo(jobInfo, controlString, jobFileReferences, jobFileLengths);
            }
            catch (FormatException)
            {
                ReturnUnsupportedPrintType(stream);
                return;
            }

            OnNewJobReceived(jobInfo);
        }

        private void ListenProc()
        {
            Listener = new TcpListener(IPAddress.Any, Config.LpdListenPort);
            Listener.Start();
            for (; ; )
            {
                var client = Listener.AcceptTcpClient();
                var stream = client.GetStream();
                ThreadPool.QueueUserWorkItem(state =>
                {
                    try
                    {
                        HandleConnection(stream);
                    }
                    catch (Exception e)
                    {
                        Logger.Error("exception thrown while handling LPD request", e);
                    }
                    finally
                    {
                        stream.Close();
                    }
                });
            }
        }

        public void Start()
        {
            Logger.Debug("starting LpdListener");
            new Thread(ListenProc).Start();
        }

        public void Stop()
        {
            Logger.Debug("stopping LpdListener");
            Listener.Stop();
        }
    }
}
