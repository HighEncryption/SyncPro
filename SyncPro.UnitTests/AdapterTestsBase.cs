namespace SyncPro.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SyncPro.Adapters;
    using SyncPro.Adapters.WindowsFileSystem;
    using SyncPro.Data;
    using SyncPro.Runtime;

    public abstract class AdapterTestsBase<TAdapter>
        where TAdapter: AdapterBase
    {
        public TestContext TestContext { get; set; }

        protected abstract TAdapter CreateSourceAdapter(SyncRelationship newRelationship, string testMethodName);

        protected abstract TAdapter CreateDestinationAdapter(SyncRelationship newRelationship, string testMethodName);

        [TestMethod]
        public void BasicSyncDownloadOnly()
        {
            if (!GlobalTestSettings.RunNetworkTests)
            {
                Assert.Inconclusive(GlobalTestSettings.NetworkTestsDisabledMessage);
            }

            string testRootPath = Path.Combine(this.TestContext.TestLogsDir, this.TestContext.TestName);
            Directory.CreateDirectory(testRootPath);

            string syncDestinationPath = Path.Combine(testRootPath, "Destination");
            Directory.CreateDirectory(syncDestinationPath);

            Global.Initialize(testRootPath, true);
            SyncRelationship newRelationship = SyncRelationship.Create();

            TAdapter sourceAdapter = CreateSourceAdapter(newRelationship, GetCurrentMethod());

            WindowsFileSystemAdapter destAdapter = new WindowsFileSystemAdapter(newRelationship);

            destAdapter.Config.RootDirectory = syncDestinationPath;

            newRelationship.Adapters.Add(sourceAdapter);
            newRelationship.Adapters.Add(destAdapter);

            newRelationship.SourceAdapter = sourceAdapter;
            newRelationship.DestinationAdapter = destAdapter;

            newRelationship.Name = "Test Relationship #1";
            newRelationship.Description = "Test Relationship Description #1";

            newRelationship.SaveAsync().Wait();

            // The list of files and folders that we expect to be present
            List<Tuple<string, long, SyncEntryType>> syncFileList = new List<Tuple<string, long, SyncEntryType>>
            {
                new Tuple<string, long, SyncEntryType>("FolderA", 0, SyncEntryType.Directory),
                new Tuple<string, long, SyncEntryType>("FolderA\\EmptyFolder", 0, SyncEntryType.Directory),
                new Tuple<string, long, SyncEntryType>("FolderA\\Book1.xlsx", 7954, SyncEntryType.File),
                new Tuple<string, long, SyncEntryType>("FolderA\\Document1.docx", 14994, SyncEntryType.File),
                new Tuple<string, long, SyncEntryType>("FolderA\\NewTextFile1.txt", 3, SyncEntryType.File),
                new Tuple<string, long, SyncEntryType>("FolderA\\NewTextFile2.txt", 3, SyncEntryType.File),
                new Tuple<string, long, SyncEntryType>("FolderA\\NewTextFile3.txt", 3, SyncEntryType.File),
                new Tuple<string, long, SyncEntryType>("FolderA\\NewTextFile4.txt", 3, SyncEntryType.File),
                new Tuple<string, long, SyncEntryType>("FolderA\\Presentation1.pptx", 30274, SyncEntryType.File),
                new Tuple<string, long, SyncEntryType>("FolderB", 0, SyncEntryType.Directory),
                new Tuple<string, long, SyncEntryType>("FolderB\\FolderC", 0, SyncEntryType.Directory),
                new Tuple<string, long, SyncEntryType>("FolderB\\FolderC\\gitignore_global.txt", 236, SyncEntryType.File),
                new Tuple<string, long, SyncEntryType>("sample_photo_00.jpg", 61813, SyncEntryType.File),
                new Tuple<string, long, SyncEntryType>("sample_photo_01.jpg", 119264, SyncEntryType.File),
                new Tuple<string, long, SyncEntryType>("sample_photo_02.jpg", 76929, SyncEntryType.File),
                new Tuple<string, long, SyncEntryType>("sample_photo_03.jpg", 35859, SyncEntryType.File)
            };

            AnalyzeJob analyzeJob = new AnalyzeJob(newRelationship);

            analyzeJob.ContinuationJob = new SyncJob(newRelationship, analyzeJob.AnalyzeResult)
            {
                TriggerType = SyncTriggerType.Manual
            };

            analyzeJob.Start();

            SyncJob syncJob = (SyncJob)analyzeJob.WaitForCompletion();

            Assert.IsTrue(syncJob.HasFinished);

            int expectedFileCount = syncFileList.Count;

            if (sourceAdapter.Configuration.DirectoriesAreUniqueEntities == false)
            {
                expectedFileCount--;
            }

            // Ensure that the right number of entries are present in the result
            Assert.AreEqual(expectedFileCount, syncJob.AnalyzeResult.AdapterResults.SelectMany(r => r.Value.EntryResults).Count());

            string[] localFiles = Directory.GetFileSystemEntries(syncDestinationPath, "*", SearchOption.AllDirectories);

            // Ensure that the number of files downloaded is the same as the number expected
            Assert.AreEqual(expectedFileCount, localFiles.Length);

            foreach (string fileSystemEntry in localFiles)
            {
                string relativePath = fileSystemEntry.Substring(syncDestinationPath.Length + 1);
                var syncFile = syncFileList.FirstOrDefault(f => f.Item1 == relativePath);

                // Ensure that the item found on disk was found in the list
                Assert.IsNotNull(syncFile);

                if (Directory.Exists(fileSystemEntry) && syncFile.Item3 != SyncEntryType.Directory)
                {
                    Assert.Fail("The item is type is incorrect (should be Directory)");
                }

                if (File.Exists(fileSystemEntry))
                {
                    if (syncFile.Item3 != SyncEntryType.File)
                    {
                        Assert.Fail("The item is type is incorrect (should be File)");
                    }

                    FileInfo f = new FileInfo(fileSystemEntry);
                    Assert.IsTrue(syncFile.Item2 >= f.Length, "File length is incorrect.");
                }
            }
        }

        [TestMethod]
        public void BasicAnalyzeOnly()
        {
            if (!GlobalTestSettings.RunNetworkTests)
            {
                Assert.Inconclusive(GlobalTestSettings.NetworkTestsDisabledMessage);
            }

            string testRootPath = Path.Combine(this.TestContext.TestLogsDir, this.TestContext.TestName);
            Directory.CreateDirectory(testRootPath);

            string syncDestinationPath = Path.Combine(testRootPath, "Destination");
            Directory.CreateDirectory(syncDestinationPath);

            Global.Initialize(testRootPath);
            SyncRelationship newRelationship = SyncRelationship.Create();

            TAdapter sourceAdapter = CreateSourceAdapter(newRelationship, GetCurrentMethod());

            WindowsFileSystemAdapter destAdapter = new WindowsFileSystemAdapter(newRelationship);
            destAdapter.Config.RootDirectory = syncDestinationPath;

            newRelationship.Adapters.Add(sourceAdapter);
            newRelationship.Adapters.Add(destAdapter);

            newRelationship.SourceAdapter = sourceAdapter;
            newRelationship.DestinationAdapter = destAdapter;

            newRelationship.Name = "Test Relationship #1";
            newRelationship.Description = "Test Relationship Description #1";

            newRelationship.SaveAsync().Wait();

            ManualResetEvent evt = new ManualResetEvent(false);

            AnalyzeJob run1 = new AnalyzeJob(newRelationship);

            run1.Finished += (sender, args) => { evt.Set(); };
            bool finished = run1.Start().Wait(60000);

            // 10min max wait time
            if (!finished)
            {
                Assert.Fail("Timeout");
            }

            Assert.IsTrue(run1.HasFinished);
        }

        [TestMethod]
        public void BasicSyncUpload()
        {
            if (!GlobalTestSettings.RunNetworkTests)
            {
                Assert.Inconclusive(GlobalTestSettings.NetworkTestsDisabledMessage);
            }

            string testRootPath = Path.Combine(this.TestContext.TestLogsDir, this.TestContext.TestName);
            Directory.CreateDirectory(testRootPath);

            string syncSourcePath = Path.Combine(testRootPath, "Source");
            Directory.CreateDirectory(syncSourcePath);

            // Create temp files/folders
            List<string> syncFileList = new List<string>
                                            {
                                                TestHelper.CreateDirectory(syncSourcePath, "dir1"),
                                                TestHelper.CreateFile(syncSourcePath, "dir1\\file1.txt"),
                                                TestHelper.CreateFile(syncSourcePath, "dir1\\file2.txt"),
                                                TestHelper.CreateFile(syncSourcePath, "dir1\\file3.txt"),
                                                TestHelper.CreateDirectory(syncSourcePath, "dir2"),
                                                TestHelper.CreateFile(syncSourcePath, "dir2\\file1.txt"),
                                                TestHelper.CreateFile(syncSourcePath, "dir2\\file2.txt"),
                                                TestHelper.CreateFile(syncSourcePath, "dir2\\file3.txt")
                                            };

            Global.Initialize(testRootPath);
            SyncRelationship newRelationship = SyncRelationship.Create();

            WindowsFileSystemAdapter sourceAdapter = new WindowsFileSystemAdapter(newRelationship);
            sourceAdapter.Config.RootDirectory = syncSourcePath;

            TAdapter destAdapter = CreateDestinationAdapter(newRelationship, GetCurrentMethod());

            newRelationship.Adapters.Add(sourceAdapter);
            newRelationship.Adapters.Add(destAdapter);

            newRelationship.SourceAdapter = sourceAdapter;
            newRelationship.DestinationAdapter = destAdapter;

            newRelationship.Name = "Test Relationship #1";
            newRelationship.Description = "Test Relationship Description #1";

            newRelationship.SaveAsync().Wait();

            AnalyzeJob analyzeJob = new AnalyzeJob(newRelationship);

            analyzeJob.ContinuationJob = new SyncJob(newRelationship, analyzeJob.AnalyzeResult)
            {
                TriggerType = SyncTriggerType.Manual
            };

            analyzeJob.Start();

            SyncJob syncJob = (SyncJob)analyzeJob.WaitForCompletion();

            Assert.IsTrue(syncJob.HasFinished);

            Assert.AreEqual(
                syncFileList.Count, 
                syncJob.AnalyzeResult.AdapterResults.SelectMany(r => r.Value.EntryResults).Count());
        }

        [TestMethod]
        public void UploadLargeFile()
        {
            if (!GlobalTestSettings.RunNetworkTests)
            {
                Assert.Inconclusive(GlobalTestSettings.NetworkTestsDisabledMessage);
            }

            string testRootPath = Path.Combine(this.TestContext.TestLogsDir, this.TestContext.TestName);
            Directory.CreateDirectory(testRootPath);

            string syncSourcePath = Path.Combine(testRootPath, "Source");
            Directory.CreateDirectory(syncSourcePath);

            // Allocate a 15M buffer
            int bufferSize = 15 * 1024 * 1024;
            byte[] data = new byte[bufferSize];

            // Fill with non-empty data
            for (int i = 0; i < bufferSize; i++)
            {
                data[i] = (byte)(i % sizeof(byte));
            }

            // Create temp files/folders
            List<string> syncFileList = new List<string>
                                            {
                                                TestHelper.CreateDirectory(syncSourcePath, "dir1"),
                                                TestHelper.CreateFile(syncSourcePath, "dir1\\bigFile.txt", data),
                                            };

            Global.Initialize(testRootPath);
            SyncRelationship newRelationship = SyncRelationship.Create();

            WindowsFileSystemAdapter sourceAdapter = new WindowsFileSystemAdapter(newRelationship);
            sourceAdapter.Config.RootDirectory = syncSourcePath;

            TAdapter destAdapter = CreateDestinationAdapter(newRelationship, GetCurrentMethod());

            newRelationship.Adapters.Add(sourceAdapter);
            newRelationship.Adapters.Add(destAdapter);

            newRelationship.SourceAdapter = sourceAdapter;
            newRelationship.DestinationAdapter = destAdapter;

            newRelationship.Name = "Test Relationship #1";
            newRelationship.Description = "Test Relationship Description #1";

            newRelationship.SaveAsync().Wait();

            AnalyzeJob analyzeJob = new AnalyzeJob(newRelationship);

            analyzeJob.ContinuationJob = new SyncJob(newRelationship, analyzeJob.AnalyzeResult)
            {
                TriggerType = SyncTriggerType.Manual
            };

            analyzeJob.Start();

            SyncJob syncJob = (SyncJob)analyzeJob.WaitForCompletion();

            AssertSyncJobSuccess(syncJob, syncFileList.Count);
        }

        [TestMethod]
        public void ExitingSyncAddFiles()
        {
            if (!GlobalTestSettings.RunNetworkTests)
            {
                Assert.Inconclusive(GlobalTestSettings.NetworkTestsDisabledMessage);
            }

            string testRootPath = Path.Combine(this.TestContext.TestLogsDir, this.TestContext.TestName);
            Directory.CreateDirectory(testRootPath);

            string syncSourcePath = Path.Combine(testRootPath, "Source");
            Directory.CreateDirectory(syncSourcePath);

            // Create temp files/folders
            List<string> syncFileList = new List<string>
                                            {
                                                TestHelper.CreateDirectory(syncSourcePath, "dir1"),
                                                TestHelper.CreateFile(syncSourcePath, "dir1\\file1.txt"),
                                                TestHelper.CreateFile(syncSourcePath, "dir1\\file2.txt"),
                                                TestHelper.CreateFile(syncSourcePath, "dir1\\file3.txt"),
                                                TestHelper.CreateDirectory(syncSourcePath, "dir2"),
                                                TestHelper.CreateFile(syncSourcePath, "dir2\\file1.txt"),
                                                TestHelper.CreateFile(syncSourcePath, "dir2\\file2.txt"),
                                                TestHelper.CreateFile(syncSourcePath, "dir2\\file3.txt")
                                            };

            Global.Initialize(testRootPath);
            SyncRelationship newRelationship = SyncRelationship.Create();

            WindowsFileSystemAdapter sourceAdapter = new WindowsFileSystemAdapter(newRelationship);
            sourceAdapter.Config.RootDirectory = syncSourcePath;

            TAdapter destAdapter = CreateDestinationAdapter(newRelationship, GetCurrentMethod());

            newRelationship.Adapters.Add(sourceAdapter);
            newRelationship.Adapters.Add(destAdapter);

            newRelationship.SourceAdapter = sourceAdapter;
            newRelationship.DestinationAdapter = destAdapter;

            newRelationship.Name = "Test Relationship #1";
            newRelationship.Description = "Test Relationship Description #1";

            newRelationship.SaveAsync().Wait();

            AnalyzeJob analyzeJob = new AnalyzeJob(newRelationship);

            analyzeJob.ContinuationJob = new SyncJob(newRelationship, analyzeJob.AnalyzeResult)
            {
                TriggerType = SyncTriggerType.Manual
            };

            analyzeJob.Start();

            SyncJob syncJob = (SyncJob)analyzeJob.WaitForCompletion();

            AssertSyncJobSuccess(syncJob, syncFileList.Count);

            syncFileList.Add(TestHelper.CreateFile(syncSourcePath, "dir1\\file4.txt"));
            syncFileList.Add(TestHelper.CreateFile(syncSourcePath, "dir2\\file4.txt"));

            AnalyzeJob analyzeJob2 = new AnalyzeJob(newRelationship);

            analyzeJob2.ContinuationJob = new SyncJob(newRelationship, analyzeJob2.AnalyzeResult)
            {
                TriggerType = SyncTriggerType.Manual
            };

            analyzeJob2.Start();

            SyncJob syncJob2 = (SyncJob)analyzeJob2.WaitForCompletion();

            AssertSyncJobSuccess(syncJob2, 2);
        }


        [TestMethod]
        public void ExitingSyncModifyFiles()
        {
            if (!GlobalTestSettings.RunNetworkTests)
            {
                Assert.Inconclusive(GlobalTestSettings.NetworkTestsDisabledMessage);
            }

            string testRootPath = Path.Combine(this.TestContext.TestLogsDir, this.TestContext.TestName);
            Directory.CreateDirectory(testRootPath);

            string syncSourcePath = Path.Combine(testRootPath, "Source");
            Directory.CreateDirectory(syncSourcePath);

            // Create temp files/folders
            List<string> syncFileList = new List<string>
                                            {
                                                TestHelper.CreateDirectory(syncSourcePath, "dir1"),
                                                TestHelper.CreateFile(syncSourcePath, "dir1\\file1.txt"),
                                                TestHelper.CreateFile(syncSourcePath, "dir1\\file2.txt"),
                                                TestHelper.CreateFile(syncSourcePath, "dir1\\file3.txt"),
                                                TestHelper.CreateDirectory(syncSourcePath, "dir2"),
                                                TestHelper.CreateFile(syncSourcePath, "dir2\\file1.txt"),
                                                TestHelper.CreateFile(syncSourcePath, "dir2\\file2.txt"),
                                                TestHelper.CreateFile(syncSourcePath, "dir2\\file3.txt")
                                            };

            Global.Initialize(testRootPath);
            SyncRelationship newRelationship = SyncRelationship.Create();

            WindowsFileSystemAdapter sourceAdapter = new WindowsFileSystemAdapter(newRelationship);
            sourceAdapter.Config.RootDirectory = syncSourcePath;

            TAdapter destAdapter = CreateDestinationAdapter(newRelationship, GetCurrentMethod());

            newRelationship.Adapters.Add(sourceAdapter);
            newRelationship.Adapters.Add(destAdapter);

            newRelationship.SourceAdapter = sourceAdapter;
            newRelationship.DestinationAdapter = destAdapter;

            newRelationship.Name = "Test Relationship #1";
            newRelationship.Description = "Test Relationship Description #1";

            newRelationship.SaveAsync().Wait();

            AnalyzeJob analyzeJob = new AnalyzeJob(newRelationship);

            analyzeJob.ContinuationJob = new SyncJob(newRelationship, analyzeJob.AnalyzeResult)
            {
                TriggerType = SyncTriggerType.Manual
            };

            analyzeJob.Start();

            SyncJob syncJob = (SyncJob)analyzeJob.WaitForCompletion();

            Assert.IsTrue(syncJob.HasFinished);

            Assert.AreEqual(
                syncFileList.Count, 
                syncJob.AnalyzeResult.AdapterResults.SelectMany(r => r.Value.EntryResults).Count());

            syncFileList.Add(TestHelper.UpdateFile(syncSourcePath, "dir1\\file1.txt"));
            syncFileList.Add(TestHelper.UpdateFile(syncSourcePath, "dir2\\file1.txt"));

            // Short sleep to ensure that the timestamp when the file is written to the remote
            // adapter differs from the initial write.
            Thread.Sleep(2000);

            AnalyzeJob analyzeJob2 = new AnalyzeJob(newRelationship);

            analyzeJob2.ContinuationJob = new SyncJob(newRelationship, analyzeJob2.AnalyzeResult)
            {
                TriggerType = SyncTriggerType.Manual
            };

            analyzeJob2.Start();

            SyncJob syncJob2 = (SyncJob)analyzeJob2.WaitForCompletion();

            Assert.IsTrue(syncJob2.HasFinished);

            AssertSyncJobSuccess(syncJob2, 2);

            AnalyzeJob analyzeJob3 = new AnalyzeJob(newRelationship);

            analyzeJob3.Start();

            analyzeJob3.WaitForCompletion();

            AssertAnalyzeJobSuccess(analyzeJob3, 0);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private string GetCurrentMethod()
        {
            var st = new StackTrace();
            var sf = st.GetFrame(1);

            return sf.GetMethod().Name;
        }

        private static void AssertSyncJobSuccess(SyncJob job, int expectedChangeCount)
        {
            Assert.IsTrue(job.HasFinished);

            Assert.AreEqual(JobResult.Success, job.JobResult);

            Assert.IsTrue(job.AnalyzeResult.IsComplete);

            Assert.AreNotEqual(DateTime.MinValue, job.StartTime);

            Assert.IsNotNull(job.EndTime);

            Assert.AreEqual(
                expectedChangeCount, 
                job.AnalyzeResult.AdapterResults.SelectMany(r => r.Value.EntryResults).Count());
        }

        private static void AssertAnalyzeJobSuccess(AnalyzeJob job, int expectedChangeCount)
        {
            Assert.IsTrue(job.HasFinished);

            Assert.AreEqual(JobResult.Success, job.JobResult);

            Assert.IsTrue(job.AnalyzeResult.IsComplete);

            Assert.AreNotEqual(DateTime.MinValue, job.StartTime);

            Assert.IsNotNull(job.EndTime);

            Assert.AreEqual(
                expectedChangeCount, 
                job.AnalyzeResult.AdapterResults.SelectMany(r => r.Value.EntryResults).Count());
        }
    }
}