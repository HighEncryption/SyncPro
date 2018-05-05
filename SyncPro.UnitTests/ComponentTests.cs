namespace SyncPro.UnitTests
{
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

            Assert.AreEqual(PathUtility.GetSegment(path1, 0), "this");
            Assert.AreEqual(PathUtility.GetSegment(path1, 1), "is");
            Assert.AreEqual(PathUtility.GetSegment(path1, 2), "a");
            Assert.AreEqual(PathUtility.GetSegment(path1, -1), "file.txt");
            Assert.AreEqual(PathUtility.GetSegment(path1, -2), "to");
        }
    }
}