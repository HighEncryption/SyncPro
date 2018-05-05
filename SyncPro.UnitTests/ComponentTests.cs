namespace SyncPro.UnitTests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using SyncPro.Utility;

    [TestClass]
    public class ComponentTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void PathUtilityGetSegmentTests()
        {
            string path1 = "this\\is\\a\\test\\path\\to\\file.txt";
            string path2 = "file.txt";

            Assert.AreEqual(PathUtility.GetSegment(path1, 0), "this");
            Assert.AreEqual(PathUtility.GetSegment(path1, 1), "is");
            Assert.AreEqual(PathUtility.GetSegment(path1, 2), "a");
            Assert.AreEqual(PathUtility.GetSegment(path1, -1), "file.txt");
            Assert.AreEqual(PathUtility.GetSegment(path1, -2), "to");

            Assert.AreEqual(PathUtility.GetSegment(path2, 0), "file.txt");
            Assert.AreEqual(PathUtility.GetSegment(path2, -1), "file.txt");
            Assert.IsTrue(Throws(() => { PathUtility.GetSegment(path2, 1);}));
            Assert.IsTrue(Throws(() => { PathUtility.GetSegment(path2, 2);}));
            Assert.IsTrue(Throws(() => { PathUtility.GetSegment(path2, -2);}));
        }

        private bool Throws(Action action)
        {
            try
            {
                action();
            }
            catch
            {
                return true;
            }

            return false;
        }
    }
}