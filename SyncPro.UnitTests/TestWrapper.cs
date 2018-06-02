namespace SyncPro.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Data.Entity;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SyncPro.Adapters;
    using SyncPro.Adapters.WindowsFileSystem;
    using SyncPro.Data;
    using SyncPro.Runtime;

    public class TestWrapper<TSource, TDestination>
        where TSource: AdapterBase
        where TDestination : AdapterBase
    {
        public string TestRootPath { get; set; }

        public TSource SourceAdapter { get; set; }

        public TDestination DestinationAdapter { get; set; }

        public SyncRelationship Relationship { get; set; }

        public List<string> SyncFileList { get; private set; }

        public TestWrapper<TSource, TDestination> SaveRelationship()
        {
            this.Relationship.SaveAsync().Wait();

            return this;
        }

        public TestWrapper<TSource, TDestination> CreateBasicSourceStructure()
        {
            WindowsFileSystemAdapter sourceAdapter = this.SourceAdapter as WindowsFileSystemAdapter;

            if (sourceAdapter == null)
            {
                throw new NotImplementedException();
            }

            if (this.SyncFileList == null)
            {
                this.SyncFileList = new List<string>();
            }

            this.SyncFileList.AddRange(new List<string>
            {
                TestHelper.CreateDirectory(sourceAdapter.Config.RootDirectory, "dir1"),
                TestHelper.CreateFile(sourceAdapter.Config.RootDirectory, "dir1\\file1.txt"),
                TestHelper.CreateFile(sourceAdapter.Config.RootDirectory, "dir1\\file2.txt"),
                TestHelper.CreateFile(sourceAdapter.Config.RootDirectory, "dir1\\file3.txt"),
                TestHelper.CreateDirectory(sourceAdapter.Config.RootDirectory, "dir2"),
                TestHelper.CreateFile(sourceAdapter.Config.RootDirectory, "dir2\\file1.txt"),
                TestHelper.CreateFile(sourceAdapter.Config.RootDirectory, "dir2\\file2.txt"),
                TestHelper.CreateFile(sourceAdapter.Config.RootDirectory, "dir2\\file3.txt"),
                TestHelper.CreateDirectory(sourceAdapter.Config.RootDirectory, "dir2\\dir3"),
                TestHelper.CreateDirectory(sourceAdapter.Config.RootDirectory, "dir2\\dir3\\dir4"),
                TestHelper.CreateFile(sourceAdapter.Config.RootDirectory, "dir2\\dir3\\dir4\\file100.txt"),
                TestHelper.CreateFile(sourceAdapter.Config.RootDirectory, "dir2\\dir3\\dir4\\file101.txt"),
                TestHelper.CreateFile(sourceAdapter.Config.RootDirectory, "dir2\\dir3\\dir4\\file102.txt"),
            });

            return this;
        }

        public TestWrapper<TSource, TDestination> CopySourceStructureToDestination()
        {
            WindowsFileSystemAdapter sourceAdapter = this.SourceAdapter as WindowsFileSystemAdapter;
            WindowsFileSystemAdapter destAdapter = this.DestinationAdapter as WindowsFileSystemAdapter;

            if (sourceAdapter == null)
            {
                throw new NotImplementedException();
            }

            if (destAdapter == null)
            {
                throw new NotImplementedException();
            }

            DirectoryCopy(
                sourceAdapter.Config.RootDirectory,
                destAdapter.Config.RootDirectory,
                true);

            return this;
        }

        public TestWrapper<TSource, TDestination> CreateSimpleSourceStructure()
        {
            WindowsFileSystemAdapter sourceAdapter = this.SourceAdapter as WindowsFileSystemAdapter;

            if (sourceAdapter == null)
            {
                throw new NotImplementedException();
            }

            if (this.SyncFileList == null)
            {
                this.SyncFileList = new List<string>();
            }

            this.SyncFileList.AddRange(new List<string>
            {
                TestHelper.CreateDirectory(sourceAdapter.Config.RootDirectory, "dir1"),
                TestHelper.CreateFile(sourceAdapter.Config.RootDirectory, "dir1\\file1.txt"),
                TestHelper.CreateFile(sourceAdapter.Config.RootDirectory, "dir1\\file2.txt"),
                TestHelper.CreateFile(sourceAdapter.Config.RootDirectory, "dir1\\file3.txt"),
            });

            return this;
        }

        public TestWrapper<TSource, TDestination> CreateBasicDestinationStructure()
        {
            WindowsFileSystemAdapter destAdapter = this.DestinationAdapter as WindowsFileSystemAdapter;

            if (destAdapter == null)
            {
                throw new NotImplementedException();
            }

            if (this.SyncFileList == null)
            {
                this.SyncFileList = new List<string>();
            }

            this.SyncFileList.AddRange(new List<string>
            {
                TestHelper.CreateDirectory(destAdapter.Config.RootDirectory, "dir10"),
                TestHelper.CreateFile(destAdapter.Config.RootDirectory, "dir10\\file1.txt"),
                TestHelper.CreateFile(destAdapter.Config.RootDirectory, "dir10\\file2.txt"),
                TestHelper.CreateFile(destAdapter.Config.RootDirectory, "dir10\\file3.txt"),
                TestHelper.CreateDirectory(destAdapter.Config.RootDirectory, "dir20"),
                TestHelper.CreateFile(destAdapter.Config.RootDirectory, "dir20\\file1.txt"),
                TestHelper.CreateFile(destAdapter.Config.RootDirectory, "dir20\\file2.txt"),
                TestHelper.CreateFile(destAdapter.Config.RootDirectory, "dir20\\file3.txt"),
                TestHelper.CreateDirectory(destAdapter.Config.RootDirectory, "dir20\\dir3"),
                TestHelper.CreateDirectory(destAdapter.Config.RootDirectory, "dir20\\dir3\\dir4"),
                TestHelper.CreateFile(destAdapter.Config.RootDirectory, "dir20\\dir3\\dir4\\file100.txt"),
                TestHelper.CreateFile(destAdapter.Config.RootDirectory, "dir20\\dir3\\dir4\\file101.txt"),
                TestHelper.CreateFile(destAdapter.Config.RootDirectory, "dir20\\dir3\\dir4\\file102.txt"),
            });

            return this;
        }

        public SyncJob CurrentSyncJob { get; set; }

        public TestRunWrapper<TSource, TDestination> CreateSyncJob()
        {
            AnalyzeJob newAnalyzeJob = new AnalyzeJob(this.Relationship);

            newAnalyzeJob.ContinuationJob = new SyncJob(this.Relationship, newAnalyzeJob.AnalyzeResult)
            {
                TriggerType = SyncTriggerType.Manual
            };

            return new TestRunWrapper<TSource, TDestination>(this, newAnalyzeJob);
        }

        public TestRunWrapper<TSource, TDestination> CreateAnalyzeJob()
        {
            return new TestRunWrapper<TSource, TDestination>(this, new AnalyzeJob(this.Relationship));
        }

        private static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            DirectoryInfo[] dirs = dir.GetDirectories();
            // If the destination directory doesn't exist, create it.
            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }
        
            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string temppath = Path.Combine(destDirName, file.Name);
                file.CopyTo(temppath, false);
            }

            // If copying subdirectories, copy them and their contents to new location.
            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string temppath = Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, temppath, true);
                }
            }
        }
    }

    public sealed class TestWrapperFactory
    {
        public static TestWrapper<WindowsFileSystemAdapter, WindowsFileSystemAdapter> CreateLocalToLocal(TestContext testContext)
        {
            string testRootPath = Path.Combine(testContext.TestLogsDir, testContext.TestName);

            if (!Directory.Exists(testRootPath))
            {
                Directory.CreateDirectory(testRootPath);
            }

            string syncSourcePath = Path.Combine(testRootPath, "Source");
            Directory.CreateDirectory(syncSourcePath);

            string syncDestinationPath = Path.Combine(testRootPath, "Destination");
            Directory.CreateDirectory(syncDestinationPath);

            TestWrapper<WindowsFileSystemAdapter, WindowsFileSystemAdapter> wrapper =
                new TestWrapper<WindowsFileSystemAdapter, WindowsFileSystemAdapter>
                {
                    TestRootPath = testRootPath
                };

            Global.Initialize(testRootPath, Debugger.IsAttached);
            wrapper.Relationship = SyncRelationship.Create();

            wrapper.SourceAdapter = new WindowsFileSystemAdapter(wrapper.Relationship);

            wrapper.SourceAdapter.Config.RootDirectory = syncSourcePath;
            wrapper.SourceAdapter.Config.IsOriginator = true;

            wrapper.DestinationAdapter = new WindowsFileSystemAdapter(wrapper.Relationship);

            wrapper.DestinationAdapter.Config.RootDirectory = syncDestinationPath;

            wrapper.Relationship.Adapters.Add(wrapper.SourceAdapter);
            wrapper.Relationship.Adapters.Add(wrapper.DestinationAdapter);

            wrapper.Relationship.SourceAdapter = wrapper.SourceAdapter;
            wrapper.Relationship.DestinationAdapter = wrapper.DestinationAdapter;

            wrapper.Relationship.Name = "Test Relationship #1";
            wrapper.Relationship.Description = "Test Relationship Description #1";

            return wrapper;
        }
    }

    public class TestRunWrapper<TSource, TDestination>
        where TSource : AdapterBase
         where TDestination : AdapterBase
    {
        private readonly TestWrapper<TSource, TDestination> testWrapper;

        public JobBase CurrentJob { get; }

        public TestRunWrapper(TestWrapper<TSource, TDestination> testWrapper, JobBase job)
        {
            this.testWrapper = testWrapper;
            this.CurrentJob = job;
        }

        public TestRunWrapper<TSource, TDestination> RunToCompletion()
        {
            this.CurrentJob.Start();
            this.CurrentJob.WaitForCompletion();

            return this;
        }

        public TestRunWrapper<TSource, TDestination> Set(Action<JobBase> run)
        {
            run(this.CurrentJob);
            return this;
        }

        public TestRunWrapper<TSource, TDestination> VerifyAnalyzeSuccess()
        {
            AnalyzeJob analyzeJob = (AnalyzeJob)this.CurrentJob;

            Assert.IsTrue(analyzeJob.HasFinished);
            Assert.IsTrue(analyzeJob.AnalyzeResult.IsComplete);

            return this;
        }

        public TestRunWrapper<TSource, TDestination> VerifySyncSuccess()
        {
            SyncJob syncJob = (SyncJob)((AnalyzeJob) this.CurrentJob).ContinuationJob;

            Assert.IsTrue(syncJob.HasFinished);
            Assert.AreEqual(JobResult.Success, syncJob.JobResult);

            return this;
        }

        public TestRunWrapper<TSource, TDestination> VerifySyncNotRun()
        {
            SyncJob syncJob = (SyncJob)((AnalyzeJob)this.CurrentJob).ContinuationJob;

            Assert.AreEqual(JobResult.NotRun, syncJob.JobResult);

            return this;
        }

        public TestRunWrapper<TSource, TDestination> VerifyEntryUpdateInfo(Func<EntryUpdateInfo, bool> predicate)
        {
            SyncJob syncJob = (SyncJob)((AnalyzeJob)this.CurrentJob).ContinuationJob;

            Assert.AreEqual(1, syncJob.AnalyzeResult.AdapterResults.Count);
            foreach (var result in syncJob.AnalyzeResult.AdapterResults.First().Value.EntryResults)
            {
                Assert.IsTrue(predicate(result));
            }

            return this;
        }

        public TestRunWrapper<TSource, TDestination> VerifyResultContainsAllFiles()
        {
            SyncJob syncJob = (SyncJob)((AnalyzeJob)this.CurrentJob).ContinuationJob;
            if (syncJob != null)
            {
                Assert.AreEqual(this.testWrapper.SyncFileList.Count, syncJob.AnalyzeResult.AdapterResults.SelectMany(r => r.Value.EntryResults).Count());
                return this;
            }

            AnalyzeJob analyzeJob = this.CurrentJob as AnalyzeJob;
            if (analyzeJob != null)
            {
                Assert.AreEqual(this.testWrapper.SyncFileList.Count, analyzeJob.AnalyzeResult.AdapterResults.SelectMany(r => r.Value.EntryResults).Count());
            }

            return this;
        }

        public TestRunWrapper<TSource, TDestination> VerifyAnalyzeEntryCount(int count)
        {
            SyncJob syncJob = (SyncJob)((AnalyzeJob)this.CurrentJob).ContinuationJob;

            Assert.AreEqual(count, syncJob.AnalyzeResult.AdapterResults.SelectMany(r => r.Value.EntryResults).Count());

            return this;
        }

        public void VerifyDatabaseHashes()
        {
            var syncFileList = this.testWrapper.SyncFileList;
            var syncSourcePath = (this.testWrapper.SourceAdapter as WindowsFileSystemAdapter).Config.RootDirectory;
            var syncDestinationPath = (this.testWrapper.DestinationAdapter as WindowsFileSystemAdapter).Config.RootDirectory;

            Dictionary<string, string[]> itemIds = new Dictionary<string, string[]>(syncFileList.Count);

            foreach (string syncFile in syncFileList)
            {
                if (syncFile.Contains("."))
                {
                    string srcId =
                        FileSystemFolder.GetUniqueIdForFileSystemInfo(
                            new FileInfo(Path.Combine(syncSourcePath, syncFile)));
                    string destId =
                        FileSystemFolder.GetUniqueIdForFileSystemInfo(
                            new FileInfo(Path.Combine(syncDestinationPath, syncFile)));
                    itemIds.Add(syncFile, new[] { srcId, destId });
                }
                else
                {
                    string srcId =
                        FileSystemFolder.GetUniqueIdForFileSystemInfo(
                            new DirectoryInfo(Path.Combine(syncSourcePath, syncFile)));
                    string destId =
                        FileSystemFolder.GetUniqueIdForFileSystemInfo(
                            new DirectoryInfo(Path.Combine(syncDestinationPath, syncFile)));
                    itemIds.Add(syncFile, new[] { srcId, destId });
                }
            }

            using (var db = this.testWrapper.Relationship.GetDatabase())
            {
                var entries = db.Entries.Include(e => e.AdapterEntries).ToList();

                Logger.Info(
                    "Source adapter has Id={0}, Dest adapter has Id={1}",
                    this.testWrapper.Relationship.Configuration.SourceAdapterId,
                    this.testWrapper.Relationship.Configuration.DestinationAdapterId);

                foreach (SyncEntry entry in entries.Where(e => e.ParentId != null))
                {
                    if (entry.State.HasFlag(SyncEntryState.IsDeleted))
                    {
                        continue;
                    }

                    string relativePath = entry.GetRelativePath(db, "\\");
                    Logger.Info("Entry Id={0}, Path={1}, Type={2}", entry.Id, relativePath, entry.Type);

                    string[] dbIds = new string[2];
                    int at = 0;

                    foreach (SyncEntryAdapterData adapterEntry in entry.AdapterEntries.OrderBy(e => e.AdapterId))
                    {
                        dbIds[at] = adapterEntry.AdapterEntryId;
                        at++;
                    }

                    string[] Ids;
                    if (itemIds.TryGetValue(relativePath, out Ids))
                    {
                        bool srcOk = string.Equals(Ids[0], dbIds[0]);
                        bool destOk = string.Equals(Ids[1], dbIds[1]);

                        Logger.Info("  Source Hash: {0}  Dest Hash: {1}",
                            srcOk ? "[OK]" : Ids[0] + " <> " + dbIds[0],
                            destOk ? "[OK]" : Ids[1] + " <> " + dbIds[1]);

                        Assert.IsTrue(srcOk, "Source hashes do not match!");
                        Assert.IsTrue(destOk, "Destination hashes do not match!");
                    }
                    else
                    {
                        Assert.Fail("Did not find item " + relativePath);
                    }

                    //string fullDestPath = Path.Combine(syncDestinationPath, relativePath);
                }
            }
        }
    }

    public static class JobExtensions
    {
        public static JobBase WaitForCompletion(this JobBase job)
        {
            // Default to 1 minute for execution
            int delayMs = 60000;

            if (Debugger.IsAttached)
            {
                delayMs *= 10;
            }

            ManualResetEvent evt = new ManualResetEvent(false);
            while (true)
            {
                job.Finished += (sender, args) =>
                {
                    evt.Set();
                };

                if (evt.WaitOne(delayMs) == false)
                {
                    Assert.Fail("Timeout");
                }

                if (job.ContinuationJob == null)
                {
                    return job;
                }

                evt.Reset();
                job = job.ContinuationJob;
            }
        }
    }
}