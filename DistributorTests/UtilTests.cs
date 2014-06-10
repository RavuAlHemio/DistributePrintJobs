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

        [TestMethod]
        public void TestNoQueryParameters()
        {
            var retDupes = Util.DecodeUriParametersAllowingDuplicates("");
            Assert.AreEqual(0, retDupes.Count);

            var retNoDupes = Util.DecodeUriParameters("");
            Assert.AreEqual(0, retNoDupes.Count);
        }

        [TestMethod]
        public void TestSingleQueryParameter()
        {
            var retDupes = Util.DecodeUriParametersAllowingDuplicates("one=two");
            Assert.AreEqual(1, retDupes.Count);
            CollectionAssert.Contains(retDupes.Keys, "one");
            Assert.AreEqual(1, retDupes["one"].Count);
            Assert.AreEqual("two", retDupes["one"][0]);

            var retNoDupes = Util.DecodeUriParameters("one=two");
            Assert.AreEqual(1, retNoDupes.Count);
            CollectionAssert.Contains(retNoDupes.Keys, "one");
            Assert.AreEqual("two", retNoDupes["one"]);
        }

        [TestMethod]
        public void TestTwoEqualSignsInQueryParameter()
        {
            var retDupes = Util.DecodeUriParametersAllowingDuplicates("one=two=three");
            Assert.AreEqual(1, retDupes.Count);
            CollectionAssert.Contains(retDupes.Keys, "one");
            Assert.AreEqual(1, retDupes["one"].Count);
            Assert.AreEqual("two=three", retDupes["one"][0]);

            var retNoDupes = Util.DecodeUriParameters("one=two=three");
            Assert.AreEqual(1, retNoDupes.Count);
            CollectionAssert.Contains(retNoDupes.Keys, "one");
            Assert.AreEqual("two=three", retNoDupes["one"]);
        }

        [TestMethod]
        public void TestDuplicateQueryParameter()
        {
            var retDupes = Util.DecodeUriParametersAllowingDuplicates("one=two&one=zero");
            Assert.AreEqual(1, retDupes.Count);
            CollectionAssert.Contains(retDupes.Keys, "one");
            Assert.AreEqual(2, retDupes["one"].Count);
            Assert.AreEqual("two", retDupes["one"][0]);
            Assert.AreEqual("zero", retDupes["one"][1]);

            var retNoDupes = Util.DecodeUriParameters("one=two&one=zero");
            Assert.AreEqual(1, retNoDupes.Count);
            CollectionAssert.Contains(retNoDupes.Keys, "one");
            Assert.AreEqual("two", retNoDupes["one"]);
        }
    }
}
