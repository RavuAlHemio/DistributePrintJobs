using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace DistributePrintJobs
{
    internal static class Util
    {
        public static Dictionary<string, string> DecodeUriParameters(string uriParameters)
        {
            var ret = new Dictionary<string, string>();

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
                var key = WebUtility.HtmlDecode(keyVal[0]);
                var val = WebUtility.HtmlDecode(keyVal[1]);
                ret[key] = val;
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
    }
}
