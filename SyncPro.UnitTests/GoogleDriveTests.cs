namespace SyncPro.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using Newtonsoft.Json;

    using SyncPro.Adapters.GoogleDrive;
    using SyncPro.Adapters.GoogleDrive.DataModel;
    using SyncPro.Adapters.WindowsFileSystem;
    using SyncPro.Configuration;
    using SyncPro.Data;
    using SyncPro.OAuth;
    using SyncPro.Runtime;

    [TestClass]
    public class GoogleDriveTests
    {
        public TestContext TestContext { get; set; }

        private static TokenResponse classCurrentToken;

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            string tokenFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "GoogleDriveTestingToken.json");

            if (!File.Exists(tokenFilePath))
            {
                throw new FileNotFoundException("Token file was not present at path " + tokenFilePath);
            }

            string tokenContent = File.ReadAllText(tokenFilePath);

            //var token = JsonConvert.DeserializeObject<TokenResponseEx>(tokenContent);
            var token = JsonConvert.DeserializeObject<TokenResponse>(tokenContent);

            if (!token.IsEncrypted)
            {
                // The token file is NOT encrypted. Immediately encrypt and  save the file back to disk 
                // for security before using it.
                token.Protect();
                tokenContent = JsonConvert.SerializeObject(token, Formatting.Indented);
                File.WriteAllText(tokenFilePath, tokenContent);
            }

            // The token file on disk is encrypted. Decrypt the values for in-memory use.
            token.Unprotect();

            using (GoogleDriveClient client = new GoogleDriveClient(token))
            {
                client.TokenRefreshed += (sender, args) =>
                {
                    //token = new TokenResponseEx(args.NewToken);
                    //token.Protect();
                    //tokenContent = JsonConvert.SerializeObject(token, Formatting.Indented);
                    //File.WriteAllText(tokenFilePath, tokenContent);
                    //token.Unprotect();

                    // The token was refreshed, so save a protected copy of the token to the token file.
                    token = args.NewToken;
                    token.SaveProtectedToken(tokenFilePath);
                };

                client.GetUserInformation().Wait();
            }

            //testContext.Properties["CurrentToken"] = token;
            classCurrentToken = token;
        }

        [TestMethod]
        public void BasicSyncLocalToGoogleDrive()
        {
            TokenResponse currentToken = this.GetCurrentToken();

            using (GoogleDriveClient client = new GoogleDriveClient(currentToken))
            {
                var res12 = client.GetItemById("0B781VIRHEt3xeDM2UkVZcDFaUTQ").Result;

                //var info = client.GetUserInformation().Result;
                var res = client.GetChildItems(new GoogleDriveAdapterItem(new Item() {Id = "0B781VIRHEt3xNGtvMUppbnpPRVk" }, null, null)).Result;

            }

            //    Assert.Inconclusive("TODO");
            //string testRootPath = Path.Combine(this.TestContext.TestLogsDir, this.TestContext.TestName);
            //Directory.CreateDirectory(testRootPath);

            //string syncSourcePath = Path.Combine(testRootPath, "Source");
            //Directory.CreateDirectory(syncSourcePath);

            //// Create temp files/folders
            //List<string> syncFileList = new List<string>
            //                                {
            //                                    TestHelper.CreateDirectory(syncSourcePath, "dir1"),
            //                                    TestHelper.CreateFile(syncSourcePath, "dir1\\file1.txt"),
            //                                    TestHelper.CreateFile(syncSourcePath, "dir1\\file2.txt"),
            //                                    TestHelper.CreateFile(syncSourcePath, "dir1\\file3.txt"),
            //                                    TestHelper.CreateDirectory(syncSourcePath, "dir2"),
            //                                    TestHelper.CreateFile(syncSourcePath, "dir2\\file1.txt"),
            //                                    TestHelper.CreateFile(syncSourcePath, "dir2\\file2.txt"),
            //                                    TestHelper.CreateFile(syncSourcePath, "dir2\\file3.txt")
            //                                };

            //TokenResponse currentToken = this.GetCurrentToken();

            //Guid remoteTestFolderName = Guid.NewGuid();
            //Item remoteTestFolder = CreateOneDriveTestDirectory(currentToken, remoteTestFolderName.ToString("D")).Result;

            //SyncRelationship newRelationship = this.SetupRelationship(testRootPath, syncSourcePath, remoteTestFolder);

            //ManualResetEvent evt = new ManualResetEvent(false);

            //SyncJob run1 = new SyncJob(newRelationship);
            //run1.SyncFinished += (sender, args) => { evt.Set(); };
            //run1.Start();

            //evt.WaitOne();

            //Assert.IsTrue(run1.HasFinished);

            //Assert.AreEqual(syncFileList.Count, run1.AnalyzeResult.EntryResults.Count);
            //OneDriveAdapter oneDriveAdapter =
            //    newRelationship.Adapters.First(a => !a.Configuration.IsOriginator) as OneDriveAdapter;

            //OneDriveClient client = new OneDriveClient(this.GetCurrentToken());
            //foreach (string syncFile in syncFileList.Where(f => f.EndsWith(".txt")))
            //{
            //    string localPath = Path.Combine(syncSourcePath, syncFile);

            //    using (var sha1 = new SHA1Managed())
            //    {
            //        byte[] content = File.ReadAllBytes(localPath);
            //        byte[] localFileHash = sha1.ComputeHash(content);

            //        var entryResult = run1.AnalyzeResult.EntryResults.FirstOrDefault(
            //            r => r.Entry.GetRelativePath(newRelationship, "\\") == syncFile);

            //        Assert.IsNotNull(entryResult);

            //        byte[] databaseHash = entryResult.Entry.Sha1Hash;

            //        Assert.AreEqual(
            //            TestHelper.HashToHex(localFileHash),
            //            TestHelper.HashToHex(databaseHash),
            //            "Local file hash does not match database hash.");

            //        Pre.Assert(oneDriveAdapter != null, "oneDriveAdapter != null");
            //        var adapterEntry =
            //            entryResult.Entry.AdapterEntries.First(e => e.AdapterId == oneDriveAdapter.Configuration.Id);
            //        string itemId = OneDriveAdapter.UniqueIdToItemId(adapterEntry.AdapterEntryId);
            //        var item = client.GetItemByItemIdAsync(itemId);
            //        var oneDriveHash = "0x" + item.Result.File.Hashes.Sha1Hash;

            //        Assert.AreEqual(
            //            TestHelper.HashToHex(localFileHash),
            //            oneDriveHash,
            //            "Local file hash does not match OneDrive hash.");
            //    }
            //}
        }

        [TestMethod]
        public void BasicSyncDownloadOnly()
        {
            string testRootPath = Path.Combine(this.TestContext.TestLogsDir, this.TestContext.TestName);
            Directory.CreateDirectory(testRootPath);

            string syncDestinationPath = Path.Combine(testRootPath, "Destination");
            Directory.CreateDirectory(syncDestinationPath);

            TokenResponse currentToken = this.GetCurrentToken();

            Global.Initialize(testRootPath);
            SyncRelationship newRelationship = SyncRelationship.Create();

            GoogleDriveAdapter sourceAdapter = new GoogleDriveAdapter(newRelationship)
            {
                TargetItemId = "0B781VIRHEt3xeGpJN1FxNGVpb28",
                CurrentToken = currentToken,
            };

            sourceAdapter.Configuration.IsOriginator = true;

            sourceAdapter.InitializeClient().Wait();

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

            ManualResetEvent evt = new ManualResetEvent(false);

            SyncJob run1 = new SyncJob(newRelationship);

            run1.Finished += (sender, args) => { evt.Set(); };
            run1.Start(SyncTriggerType.Manual);

            // 10min max wait time
            if (evt.WaitOne(600000) == false)
            {
                Assert.Fail("Timeout");
            }

            Assert.IsTrue(run1.HasFinished);

            // Ensure that the right number of entries are present in the result
            Assert.AreEqual(syncFileList.Count, run1.AnalyzeResult.AdapterResults.SelectMany(r => r.Value.EntryResults).Count());

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

        private TokenResponse GetCurrentToken()
        {
            //TokenResponse currentToken = this.TestContext.Properties["CurrentToken"] as TokenResponse;

            if (classCurrentToken == null)
            {
                Assert.Inconclusive("Token not initialized.");
            }

            return classCurrentToken;
        }

    }
}