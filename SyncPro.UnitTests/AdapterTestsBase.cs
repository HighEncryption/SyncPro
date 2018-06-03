namespace SyncPro.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
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

        protected abstract TAdapter CreateSourceAdapter_BasicSyncDownloadOnly(SyncRelationship newRelationship);

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

            Global.Initialize(testRootPath);
            SyncRelationship newRelationship = SyncRelationship.Create();


            TAdapter sourceAdapter = this.CreateSourceAdapter_BasicSyncDownloadOnly(newRelationship);

            //sourceAdapter.Configuration.IsOriginator = true;

            //sourceAdapter.InitializeClient().Wait();

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

            // Ensure that the right number of entries are present in the result
            Assert.AreEqual(syncFileList.Count, syncJob.AnalyzeResult.AdapterResults.SelectMany(r => r.Value.EntryResults).Count());

            string[] localFiles = Directory.GetFileSystemEntries(syncDestinationPath, "*", SearchOption.AllDirectories);

            // Ensure that the number of files downloaded is the same as the number expected
            Assert.AreEqual(syncFileList.Count, localFiles.Length);

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
        
        protected abstract TAdapter CreateSourceAdapter_BasicAnalyzeOnly(SyncRelationship newRelationship);


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

            TAdapter sourceAdapter = CreateSourceAdapter_BasicAnalyzeOnly(newRelationship);

            //TokenResponse currentToken = this.GetCurrentToken();

            //Global.Initialize(testRootPath);
            //SyncRelationship newRelationship = SyncRelationship.Create();

            //OneDriveAdapter sourceAdapter = new OneDriveAdapter(newRelationship)
            //{
            //    CurrentToken = currentToken,
            //};

            //sourceAdapter.Configuration.IsOriginator = true;
            //sourceAdapter.Config.TargetPath = "OneDrive/SyncTest";
            //sourceAdapter.InitializeClient().Wait();

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
    }
}