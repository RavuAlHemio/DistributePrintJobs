// Released into the public domain.
// http://creativecommons.org/publicdomain/zero/1.0/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using log4net;

namespace DistributePrintJobs
{
    public static class Util
    {
        public static bool IsResettable(this JobInfo.JobStatus status)
        {
            return (status != JobInfo.JobStatus.Unknown && status != JobInfo.JobStatus.ReadyToPrint);
        }

        public static bool IsSendable(this JobInfo.JobStatus status)
        {
            return (status != JobInfo.JobStatus.Unknown);
        }

        public static Dictionary<string, string> DecodeUriParameters(string uriParameters)
        {
            var listRet = DecodeUriParametersAllowingDuplicates(uriParameters);
            var ret = new Dictionary<string, string>();
            foreach (var pair in listRet)
            {
                ret[pair.Key] = pair.Value[0];
            }
            return ret;
        }

        public static Dictionary<string, List<string>> DecodeUriParametersAllowingDuplicates(string uriParameters)
        {
            var ret = new Dictionary<string, List<string>>();

            // key-value pairs are split using ampersands
            var keyValuePairs = uriParameters.Split('&');

            foreach (var keyValuePair in keyValuePairs)
            {
                // key and value are split on the first equals sign
                var keyVal = keyValuePair.Split(new char[] { '=' }, 2);
                if (keyVal.Length != 2)
                {
                    continue;
                }

                // decode key and value
                var key = WebUtility.UrlDecode(keyVal[0]);
                var val = WebUtility.UrlDecode(keyVal[1]);

                if (!ret.ContainsKey(key))
                {
                    ret[key] = new List<string>();
                }
                ret[key].Add(val);
            }

            return ret;
        }

        public static long CopyStream(Stream source, Stream destination, long? maxLength)
        {
            long totalReadBytes = 0;
            var buffer = new byte[1024];

            for (; ; )
            {
                int howMany = buffer.Length;
                if (maxLength.HasValue && maxLength.Value - totalReadBytes < buffer.Length)
                {
                    howMany = (int)(maxLength.Value - totalReadBytes);
                }

                var readBytes = source.Read(buffer, 0, howMany);
                if (readBytes == 0)
                {
                    return totalReadBytes;
                }

                destination.Write(buffer, 0, readBytes);
                totalReadBytes += readBytes;

                if (maxLength.HasValue && maxLength.Value == totalReadBytes)
                {
                    return totalReadBytes;
                }
            }
        }

        private static void EventLogEntryMapping(log4net.Appender.EventLogAppender appender, log4net.Core.Level log4netLevel, EventLogEntryType eventLogLevel)
        {
            var mapping = new log4net.Appender.EventLogAppender.Level2EventLogEntryType();
            mapping.EventLogEntryType = eventLogLevel;
            mapping.Level = log4netLevel;
            appender.AddMapping(mapping);
        }

        public static void SetupLogging()
        {
            var confFile = new FileInfo(Path.Combine(Util.ProgramDirectory, "Logging.conf"));
            if (confFile.Exists)
            {
                log4net.Config.XmlConfigurator.Configure(confFile);
            }
            else
            {
                var rootLogger = ((log4net.Repository.Hierarchy.Hierarchy)log4net.LogManager.GetRepository()).Root;
                rootLogger.Level = log4net.Core.Level.Debug;

                // log WARN and above to NT Event Log
                var eventLogAppender = new log4net.Appender.EventLogAppender();
                eventLogAppender.ApplicationName = "DistributePrintJobs";
                eventLogAppender.LogName = "Application";
                EventLogEntryMapping(eventLogAppender, log4net.Core.Level.Debug, EventLogEntryType.Information);
                EventLogEntryMapping(eventLogAppender, log4net.Core.Level.Info, EventLogEntryType.Information);
                EventLogEntryMapping(eventLogAppender, log4net.Core.Level.Warn, EventLogEntryType.Warning);
                EventLogEntryMapping(eventLogAppender, log4net.Core.Level.Error, EventLogEntryType.Error);
                EventLogEntryMapping(eventLogAppender, log4net.Core.Level.Fatal, EventLogEntryType.Error);
                rootLogger.AddAppender(eventLogAppender);
            }
        }

        public static string ProgramDirectory
        {
            get
            {
                var localPath = (new Uri(System.Reflection.Assembly.GetExecutingAssembly().CodeBase)).LocalPath;
                return Path.GetDirectoryName(localPath);
            }
        }
    }
}
