﻿namespace SyncPro.UnitTests
{
    using System.IO;

    using JsonLog;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SyncPro.Adapters;

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
        public void SyncCreationTimestampChange()
        {
            var testWrapper = TestWrapperFactory
                .CreateLocalToLocal(this.TestContext)
                .SaveRelationship()
                .CreateSimpleSourceStructure();

            // First sync run
            testWrapper
                .CreateSyncRun()
                .RunToCompletion()
                .VerifySyncSuccess()
                .VerifyResultContainsAllFiles()
                .VerifyDatabaseHashes();

            Logger.Info("Logging database before creation timestamp change:");
            using (var db = testWrapper.Relationship.GetDatabase())
            {
                TestHelper.LogConfiguration(testWrapper.Relationship.Configuration);
                TestHelper.LogDatabase(db);
            }

            // Update the creation time of a file
            string filePath = Path.Combine(testWrapper.SourceAdapter.Config.RootDirectory, "dir1\\file1.txt");
            var newCreationTime = File.GetCreationTimeUtc(filePath).AddSeconds(1);
            File.SetCreationTimeUtc(filePath, newCreationTime);

            // Second sync run
            var syncRun = testWrapper
                .CreateSyncRun();

            syncRun
                .RunToCompletion()
                .VerifySyncSuccess()
                .VerifyAnalyzeEntryCount(1)
                .VerifyDatabaseHashes();

            // Verify that only the created timestamp change was detected
            var changedSyncEntry = syncRun.CurrentSyncRun.AnalyzeResult.AdapterResults[1].EntryResults[0];
            Assert.AreEqual(changedSyncEntry.Flags, SyncEntryChangedFlags.CreatedTimestamp);

            // Verify that the timestamp was copied
            filePath = Path.Combine(testWrapper.DestinationAdapter.Config.RootDirectory, "dir1\\file1.txt");
            var expectedCreationTime = File.GetCreationTimeUtc(filePath);
            Assert.AreEqual(expectedCreationTime, newCreationTime);
        }

        [TestMethod]
        public void SyncModifiedTimestampChange()
        {
            var testWrapper = TestWrapperFactory
                .CreateLocalToLocal(this.TestContext)
                .SaveRelationship()
                .CreateSimpleSourceStructure();

            // First sync run
            testWrapper
                .CreateSyncRun()
                .RunToCompletion()
                .VerifySyncSuccess()
                .VerifyResultContainsAllFiles()
                .VerifyDatabaseHashes();

            Logger.Info("Logging database before last modified timestamp change:");
            using (var db = testWrapper.Relationship.GetDatabase())
            {
                TestHelper.LogConfiguration(testWrapper.Relationship.Configuration);
                TestHelper.LogDatabase(db);
            }

            // Update the creation time of a file
            string filePath = Path.Combine(testWrapper.SourceAdapter.Config.RootDirectory, "dir1\\file1.txt");
            var newModifiedTime = File.GetLastWriteTimeUtc(filePath).AddSeconds(1);
            File.SetLastWriteTimeUtc(filePath, newModifiedTime);

            // Second sync run
            var syncRun = testWrapper
                .CreateSyncRun();

            syncRun
                .RunToCompletion()
                .VerifySyncSuccess()
                .VerifyAnalyzeEntryCount(1)
                .VerifyDatabaseHashes();

            // Verify that only the created timestamp change was detected
            var changedSyncEntry = syncRun.CurrentSyncRun.AnalyzeResult.AdapterResults[1].EntryResults[0];
            Assert.AreEqual(changedSyncEntry.Flags, SyncEntryChangedFlags.ModifiedTimestamp);

            // Verify that the timestamp was copied
            filePath = Path.Combine(testWrapper.DestinationAdapter.Config.RootDirectory, "dir1\\file1.txt");
            var expectedModifiedTime = File.GetLastWriteTimeUtc(filePath);
            Assert.AreEqual(expectedModifiedTime, newModifiedTime);
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