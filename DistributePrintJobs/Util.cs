using System;
using System.Collections.Generic;
using System.Net;

namespace DistributePrintJobs
{
    static class Util
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
        }
    }
}
