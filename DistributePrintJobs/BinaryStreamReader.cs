using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DistributePrintJobs
{
    class BinaryStreamReader
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
