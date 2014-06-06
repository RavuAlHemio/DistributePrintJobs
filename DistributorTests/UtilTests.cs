using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DistributePrintJobs;

namespace DistributorTests
{
    [TestClass]
    public class UtilTests
    {
        [TestMethod]
        public void TestCopyStream()
        {
            var inStream = new MemoryStream();
            var outStream = new MemoryStream();
            var rnd = new Random();

            var length = (int)(rnd.NextDouble() * 65536);
            var inBytes = new byte[length];

            for (int i = 0; i < length; ++i)
            {
                var b = (byte)rnd.Next(0, 256);
                inBytes[i] = b;
                inStream.WriteByte(b);
            }

            inStream.Seek(0, SeekOrigin.Begin);
            var copied = Util.CopyStream(inStream, outStream, null);

            Assert.AreEqual(length, copied);
            Assert.AreEqual(length, outStream.Length);

            CollectionAssert.AreEqual(inBytes, outStream.GetBuffer().Take(length).ToArray());
        }
    }
}
