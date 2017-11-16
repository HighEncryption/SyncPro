namespace SyncPro.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Data.Entity;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using JsonLog;

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

        public TestWrapper<TSource, TDestination> CreateLargeSourceStructure()
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

            string testRoot = "C:\\large";

            Random r2 = new Random();
            // Generate the random data source file (32k)
            byte[] dataSource = new byte[1024 * 32];
            r2.NextBytes(dataSource);

            List<int> ints = new List<int>();
            for (int i = 0; i < 100; i++)
            {
                ints.Add(i);
            }

            //for (int i = 0; i < 100; i++)
            Parallel.ForEach(
                ints,
                new ParallelOptions() {  MaxDegreeOfParallelism = 8 },
                (i, state) =>
                {
                    Random r = new Random();
                    string suffix = Guid.NewGuid().ToString("N").Substring(0, r.Next(4, 32));
                    string folderName1 = string.Format("dir{0}_{1}", i, suffix);
                    string path1 = TestHelper.CreateDirectory(testRoot, folderName1);
                    this.SyncFileList.Add(path1);

                    for (int j = 0; j < 100; j++)
                    {
                        suffix = Guid.NewGuid().ToString("N").Substring(0, r.Next(4, 32));
                        string folderName2 = string.Format("dir{0}_{1}", j, suffix);
                        string path2 = TestHelper.CreateDirectory(testRoot, Path.Combine(folderName1, folderName2));

                        for (int k = 0; k < 10; k++)
                        {
                            suffix = Guid.NewGuid().ToString("N").Substring(0, r.Next(4, 32));
                            string fileName = string.Format("file{0}_{1}", k, suffix);

                            string fullPath = Path.Combine(testRoot, path2, fileName);

                            using (FileStream fs = File.Open(fullPath, FileMode.CreateNew))
                            {
                                using (BinaryWriter sw = new BinaryWriter(fs))
                                {
                                    sw.Write(dataSource, 0, r.Next(0, dataSource.Length - 1));
                                }
                            }
                        }
                    }

                }
            );

            //this.SyncFileList.AddRange(new List<string>
            //{
            //    TestHelper.CreateDirectory(sourceAdapter.Config.RootDirectory, "dir1"),
            //    TestHelper.CreateFile(sourceAdapter.Config.RootDirectory, "dir1\\file1.txt"),
            //    TestHelper.CreateFile(sourceAdapter.Config.RootDirectory, "dir1\\file2.txt"),
            //    TestHelper.CreateFile(sourceAdapter.Config.RootDirectory, "dir1\\file3.txt"),
            //    TestHelper.CreateDirectory(sourceAdapter.Config.RootDirectory, "dir2"),
            //    TestHelper.CreateFile(sourceAdapter.Config.RootDirectory, "dir2\\file1.txt"),
            //    TestHelper.CreateFile(sourceAdapter.Config.RootDirectory, "dir2\\file2.txt"),
            //    TestHelper.CreateFile(sourceAdapter.Config.RootDirectory, "dir2\\file3.txt"),
            //    TestHelper.CreateDirectory(sourceAdapter.Config.RootDirectory, "dir2\\dir3"),
            //    TestHelper.CreateDirectory(sourceAdapter.Config.RootDirectory, "dir2\\dir3\\dir4"),
            //    TestHelper.CreateFile(sourceAdapter.Config.RootDirectory, "dir2\\dir3\\dir4\\file100.txt"),
            //    TestHelper.CreateFile(sourceAdapter.Config.RootDirectory, "dir2\\dir3\\dir4\\file101.txt"),
            //    TestHelper.CreateFile(sourceAdapter.Config.RootDirectory, "dir2\\dir3\\dir4\\file102.txt"),
            //});

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

        public SyncRun CurrentSyncRun { get; set; }

        public TestRunWrapper<TSource, TDestination> CreateSyncRun()
        {
            return new TestRunWrapper<TSource, TDestination>(this);
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

            Global.Initialize(testRootPath);
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

        public SyncRun CurrentSyncRun { get; }

        public TestRunWrapper(TestWrapper<TSource, TDestination> testWrapper)
        {
            this.testWrapper = testWrapper;
            this.CurrentSyncRun = new SyncRun(testWrapper.Relationship);
        }

        public TestRunWrapper<TSource, TDestination> RunToCompletion()
        {
            ManualResetEvent evt = new ManualResetEvent(false);

            this.CurrentSyncRun.SyncFinished += (sender, args) =>
            {
                evt.Set();
            };
            this.CurrentSyncRun.Start(SyncTriggerType.Manual);

            // 1 min max wait time
            if (evt.WaitOne(60000) == false)
            {
                Assert.Fail("Timeout");
            }

            return this;
        }

        public TestRunWrapper<TSource, TDestination> Set(Action<SyncRun> run)
        {
            run(this.CurrentSyncRun);
            return this;
        }

        public TestRunWrapper<TSource, TDestination> VerifyAnalyzeSuccess()
        {
            Assert.IsTrue(this.CurrentSyncRun.HasFinished);
            Assert.IsTrue(this.CurrentSyncRun.AnalyzeResult.IsComplete);

            return this;
        }

        public TestRunWrapper<TSource, TDestination> VerifySyncSuccess()
        {
            Assert.IsTrue(this.CurrentSyncRun.HasFinished);
            Assert.AreEqual(SyncRunResult.Success, this.CurrentSyncRun.SyncResult);

            return this;
        }

        public TestRunWrapper<TSource, TDestination> VerifySyncNotRun()
        {
            Assert.AreEqual(SyncRunResult.NotRun, this.CurrentSyncRun.SyncResult);

            return this;
        }

        public TestRunWrapper<TSource, TDestination> VerifyResultContainsAllFiles()
        {
            Assert.AreEqual(this.testWrapper.SyncFileList.Count, this.CurrentSyncRun.AnalyzeResult.AdapterResults.SelectMany(r => r.Value.EntryResults).Count());

            return this;
        }

        public TestRunWrapper<TSource, TDestination> VerifyAnalyzeEntryCount(int count)
        {
            Assert.AreEqual(count, this.CurrentSyncRun.AnalyzeResult.AdapterResults.SelectMany(r => r.Value.EntryResults).Count());

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

}