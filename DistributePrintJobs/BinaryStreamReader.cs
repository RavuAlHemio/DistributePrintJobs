// Released into the public domain.
// http://creativecommons.org/publicdomain/zero/1.0/

using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DistributePrintJobs
{
    static class BinaryStreamReader
    {
        public static byte[] ReadStreamToEnd(Stream stream)
        {
            var ret = new List<byte>();
            var buffer = new byte[1024];

            for (; ; )
            {
                var bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                {
                    return ret.ToArray();
                }
                ret.AddRange(buffer.Take(bytesRead));
            }
        }
    }
}
