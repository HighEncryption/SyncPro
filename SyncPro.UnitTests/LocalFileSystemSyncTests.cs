namespace SyncPro.UnitTests
{
    using System.IO;

    using JsonLog;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class LocalFileSystemSyncTests
    {
        public TestContext TestContext { get; set; }

        public void TestCleanup()
        {
            Logger.Info("Test completed");
        }

        [TestMethod]
        public void BasicAnalyzeOnly()
        {
            var testWrapper = TestWrapperFactory
                .CreateLocalToLocal(this.TestContext)
                .SaveRelationship()
                .CreateBasicSourceStructure();

            testWrapper
                .CreateSyncRun()
                .Set(r => r.AnalyzeOnly = true)
                .RunToCompletion()
                .VerifyAnalyzeSuccess()
                .VerifyResultContainsAllFiles();

            // Ensure that the destination directory is empty (no files should be copied in analyze-only)
            var destFiles = Directory.GetFileSystemEntries(testWrapper.DestinationAdapter.Config.RootDirectory);
            Assert.AreEqual(0, destFiles.Length);
        }

        [TestMethod]
        public void BasicSyncOnly()
        {
            TestWrapperFactory
                .CreateLocalToLocal(this.TestContext)
                .SaveRelationship()
                .CreateBasicSourceStructure()
                .CreateSyncRun()
                .RunToCompletion()
                .VerifySyncSuccess()
                .VerifyResultContainsAllFiles()
                .VerifyDatabaseHashes();
        }

        [TestMethod]
        public void SyncIsIdempotent()
        {
            var testWrapper = TestWrapperFactory
                .CreateLocalToLocal(this.TestContext)
                .SaveRelationship()
                .CreateBasicSourceStructure();

            // First sync run
            testWrapper
                .CreateSyncRun()
                .RunToCompletion()
                .VerifySyncSuccess()
                .VerifyResultContainsAllFiles()
                .VerifyDatabaseHashes();

            // Second sync run
            testWrapper
                .CreateSyncRun()
                .RunToCompletion()
                .VerifyAnalyzeSuccess()
                .VerifySyncNotRun()
                .VerifyAnalyzeEntryCount(0);
        }

        [TestMethod]
        public void SyncWithDelete()
        {
            var testWrapper = TestWrapperFactory
                .CreateLocalToLocal(this.TestContext)
                .SaveRelationship()
                .CreateBasicSourceStructure();

            // First sync run
            testWrapper
                .CreateSyncRun()
                .RunToCompletion()
                .VerifySyncSuccess()
                .VerifyResultContainsAllFiles()
                .VerifyDatabaseHashes();

            Logger.Info("Logging database before delete:");
            using (var db = testWrapper.Relationship.GetDatabase())
            {
                TestHelper.LogConfiguration(testWrapper.Relationship.Configuration);
                TestHelper.LogDatabase(db);
            }

            // Delete dir2 and a file from dir1
            var syncSourcePath = testWrapper.SourceAdapter.Config.RootDirectory;
            File.Delete(Path.Combine(syncSourcePath, "dir1\\file2.txt"));
            Directory.Delete(Path.Combine(syncSourcePath, "dir2\\dir3"), true);

            testWrapper.SyncFileList.Remove("dir1\\file2.txt");
            testWrapper.SyncFileList.RemoveAll(e => e.StartsWith("dir2\\dir3"));

            // Second sync run
            testWrapper
                .CreateSyncRun()
                .RunToCompletion()
                .VerifySyncSuccess()
                .VerifyAnalyzeEntryCount(6)
                .VerifyDatabaseHashes();
        }

        [TestMethod]
        public void SyncTimestampChange()
        {
            var testWrapper = TestWrapperFactory
                .CreateLocalToLocal(this.TestContext)
                .SaveRelationship()
                .CreateBasicSourceStructure();

            // First sync run
            testWrapper
                .CreateSyncRun()
                .RunToCompletion()
                .VerifySyncSuccess()
                .VerifyResultContainsAllFiles()
                .VerifyDatabaseHashes();

            Logger.Info("Logging database before delete:");
            using (var db = testWrapper.Relationship.GetDatabase())
            {
                TestHelper.LogConfiguration(testWrapper.Relationship.Configuration);
                TestHelper.LogDatabase(db);
            }

            // Delete dir2 and a file from dir1
            var syncSourcePath = testWrapper.SourceAdapter.Config.RootDirectory;
            File.AppendAllLines(
                Path.Combine(syncSourcePath, "dir1\\file2.txt"),
                new [] { "Appended line." });

            //File.Move(
            //    Path.Combine(syncSourcePath, "dir1\\file3.txt"),
            //    Path.Combine(syncSourcePath, "dir1\\file3_rename.txt"));

            // Second sync run
            testWrapper
                .CreateSyncRun()
                .RunToCompletion()
                .VerifySyncSuccess()
                .VerifyAnalyzeEntryCount(1)
                .VerifyDatabaseHashes();
        }

        [TestMethod]
        public void BasicBidirectionalSync()
        {
            var wrapper = TestWrapperFactory
                .CreateLocalToLocal(this.TestContext);

            wrapper.Relationship.SyncScope = SyncScopeType.Bidirectional;

            wrapper
                .SaveRelationship()
                .CreateBasicSourceStructure()
                .CreateBasicDestinationStructure()
                .CreateSyncRun()
                .RunToCompletion()
                .VerifySyncSuccess()
                .VerifyResultContainsAllFiles()
                .VerifyDatabaseHashes();
        }
    }
}