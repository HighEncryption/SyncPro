namespace SyncPro.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using Newtonsoft.Json;

    using SyncPro.Adapters;
    using SyncPro.Adapters.MicrosoftOneDrive;
    using SyncPro.Adapters.MicrosoftOneDrive.DataModel;
    using SyncPro.Adapters.WindowsFileSystem;
    using SyncPro.Data;
    using SyncPro.OAuth;
    using SyncPro.Runtime;

    [TestClass]
    public class OneDriveSyncTests
    {
        public TestContext TestContext { get; set; }

        private static TokenResponse classCurrentToken;

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            if (!GlobalTestSettings.RunNetworkTests)
            {
                return;
            }

            string tokenFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "OneDriveTestingToken.json");

            if (!File.Exists(tokenFilePath))
            {
                string tokenHelperPath = "OneDriveTokenHelper.exe";
                Process p = Process.Start(tokenHelperPath, "/getToken /path \"" + tokenFilePath + "\"");
                p.WaitForExit();

                if (p.ExitCode != 1)
                {
                    throw new Exception("Failed to get token using OneDriveTokenHelper");
                }

                //throw new FileNotFoundException("Token file was not present at path " + tokenFilePath);
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

            using (OneDriveClient client = new OneDriveClient(token))
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

                client.GetUserProfileAsync().Wait();
            }

            //testContext.Properties["CurrentToken"] = token;
            classCurrentToken = token;
        }

        [TestMethod]
        public void BasicSyncLocalToOneDrive()
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

            TokenResponse currentToken = this.GetCurrentToken();

            Guid remoteTestFolderName = Guid.NewGuid();
            Item remoteTestFolder = CreateOneDriveTestDirectory(currentToken, remoteTestFolderName.ToString("D")).Result;

            SyncRelationship newRelationship = this.SetupRelationship(testRootPath, syncSourcePath, remoteTestFolder);

            AnalyzeJob analyzeJob = new AnalyzeJob(newRelationship);

            analyzeJob.ContinuationJob = new SyncJob(newRelationship, analyzeJob.AnalyzeResult)
            {
                TriggerType = SyncTriggerType.Manual
            };

            analyzeJob.Start();

            SyncJob syncJob = (SyncJob)analyzeJob.WaitForCompletion();

            Assert.IsTrue(syncJob.HasFinished);

            Assert.AreEqual(syncFileList.Count, syncJob.AnalyzeResult.AdapterResults.SelectMany(r => r.Value.EntryResults).Count());
            OneDriveAdapter oneDriveAdapter =
                newRelationship.Adapters.First(a => !a.Configuration.IsOriginator) as OneDriveAdapter;

            OneDriveClient client = new OneDriveClient(this.GetCurrentToken());
            foreach (string syncFile in syncFileList.Where(f => f.EndsWith(".txt")))
            {
                string localPath = Path.Combine(syncSourcePath, syncFile);

                using (var sha1 = new SHA1Managed())
                {
                    byte[] content = File.ReadAllBytes(localPath);
                    byte[] localFileHash = sha1.ComputeHash(content);

                    EntryUpdateInfo entryResult;
                    using (var db = newRelationship.GetDatabase())
                    {
                        entryResult = syncJob.AnalyzeResult.AdapterResults.SelectMany(r => r.Value.EntryResults).FirstOrDefault(
                            r => r.Entry.GetRelativePath(db, "\\") == syncFile);
                    }

                    Assert.IsNotNull(entryResult);

                    byte[] databaseHash = entryResult.Entry.OriginalSha1Hash;

                    Assert.AreEqual(
                        TestHelper.HashToHex(localFileHash),
                        TestHelper.HashToHex(databaseHash), 
                        "Local file hash does not match database hash.");

                    Pre.Assert(oneDriveAdapter != null, "oneDriveAdapter != null");
                    var adapterEntry =
                        entryResult.Entry.AdapterEntries.First(e => e.AdapterId == oneDriveAdapter.Configuration.Id);
                    string itemId = adapterEntry.AdapterEntryId;
                    var item = client.GetItemByItemIdAsync(itemId);
                    var oneDriveHash = "0x" + item.Result.File.Hashes.Sha1Hash;

                    Assert.AreEqual(
                        TestHelper.HashToHex(localFileHash),
                        oneDriveHash,
                        "Local file hash does not match OneDrive hash.");
                }
            }
        }

        /// <summary>
        /// Test uploading a file that requires multiple fragments to be sent.
        /// </summary>
        [TestMethod]
        public void OneDriveFileUploadMultipleFragments()
        {
            if (!GlobalTestSettings.RunNetworkTests)
            {
                Assert.Inconclusive(GlobalTestSettings.NetworkTestsDisabledMessage);
            }

            int fragmentSize = 327680; // 320k
            int payloadSize = 819200; // 2.5 fragments

            TokenResponse currentToken = this.GetCurrentToken();

            Guid remoteTestFolderName = Guid.NewGuid();
            Item remoteTestFolder = CreateOneDriveTestDirectory(currentToken, remoteTestFolderName.ToString("D")).Result;

            using (OneDriveClient client = new OneDriveClient(currentToken))
            {
                OneDriveUploadSession session =
                    client.CreateUploadSession(remoteTestFolder.Id, "uploadTest.txt", payloadSize).Result;

                using (OneDriveFileUploadStream stream = new OneDriveFileUploadStream(client, session, fragmentSize))
                {
                    byte[] data = CreateUploadBuffer(payloadSize);
                    stream.Write(data, 0, data.Length);
                }

                Assert.AreEqual(OneDriveFileUploadState.Completed, session.State);
                Assert.IsNotNull(session.Item);
            }
        }

        /// <summary>
        /// Test uploading a file that requires multiple fragments to be sent.
        /// </summary>
        [TestMethod]
        public void OneDriveFileUploadSingleFragment()
        {
            if (!GlobalTestSettings.RunNetworkTests)
            {
                Assert.Inconclusive(GlobalTestSettings.NetworkTestsDisabledMessage);
            }

            int fragmentSize = OneDriveFileUploadStream.DefaultFragmentSize; // 10M
            int payloadSize = 524288; // < 1 fragments

            TokenResponse currentToken = this.GetCurrentToken();

            Guid remoteTestFolderName = Guid.NewGuid();
            Item remoteTestFolder = CreateOneDriveTestDirectory(currentToken, remoteTestFolderName.ToString("D")).Result;

            using (OneDriveClient client = new OneDriveClient(currentToken))
            {
                OneDriveUploadSession session =
                    client.CreateUploadSession(remoteTestFolder.Id, "uploadTest.txt", payloadSize).Result;

                using (OneDriveFileUploadStream stream = new OneDriveFileUploadStream(client, session, fragmentSize))
                {
                    byte[] data = CreateUploadBuffer(payloadSize);
                    stream.Write(data, 0, data.Length);
                }

                Assert.AreEqual(OneDriveFileUploadState.Completed, session.State);
                Assert.IsNotNull(session.Item);
            }
        }

        /// <summary>
        /// Test uploading a file that requires multiple fragments to be sent.
        /// </summary>
        [TestMethod]
        public void OneDriveFileUploadMultipleWrites()
        {
            if (!GlobalTestSettings.RunNetworkTests)
            {
                Assert.Inconclusive(GlobalTestSettings.NetworkTestsDisabledMessage);
            }

            int fragmentSize = 327680; // 320k
            int payloadSize = 819200; // 2.5 fragments

            TokenResponse currentToken = this.GetCurrentToken();

            Guid remoteTestFolderName = Guid.NewGuid();
            Item remoteTestFolder = CreateOneDriveTestDirectory(currentToken, remoteTestFolderName.ToString("D")).Result;

            using (OneDriveClient client = new OneDriveClient(currentToken))
            {
                OneDriveUploadSession session =
                    client.CreateUploadSession(remoteTestFolder.Id, "uploadTest.txt", payloadSize).Result;

                using (OneDriveFileUploadStream stream = new OneDriveFileUploadStream(client, session, fragmentSize))
                {
                    // Create a buffer for the data we want to send, and fill it with ASCII text
                    byte[] data = CreateUploadBuffer(payloadSize);

                    // Write the data in 1k chunks
                    int writeSize = 1024;
                    for (int j = 0; j < payloadSize; j += writeSize)
                    {
                        stream.Write(data, j, writeSize);
                    }
                }

                Assert.AreEqual(OneDriveFileUploadState.Completed, session.State);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(OneDriveException), "More data was written to the stream than is allowed by the file.")]
        public void OneDriveFileUploadTooMuchData()
        {
            if (!GlobalTestSettings.RunNetworkTests)
            {
                Assert.Inconclusive(GlobalTestSettings.NetworkTestsDisabledMessage);
            }

            int payloadSize = 262144; // 256k

            TokenResponse currentToken = this.GetCurrentToken();

            Guid remoteTestFolderName = Guid.NewGuid();
            Item remoteTestFolder = CreateOneDriveTestDirectory(currentToken, remoteTestFolderName.ToString("D")).Result;

            using (OneDriveClient client = new OneDriveClient(currentToken))
            {
                // Specify the upload size as 16 bytes smaller than we are actually going to send.
                OneDriveUploadSession session =
                    client.CreateUploadSession(remoteTestFolder.Id, "uploadTest.txt", payloadSize - 16).Result;

                using (OneDriveFileUploadStream stream = new OneDriveFileUploadStream(client, session))
                {
                    byte[] data = CreateUploadBuffer(payloadSize);

                    // Write the data in 1k chunks
                    int writeSize = 1024;
                    for (int j = 0; j < payloadSize; j += writeSize)
                    {
                        stream.Write(data, j, writeSize);
                    }
                }
            }
        }

        [TestMethod]
        public void OneDriveFileUploadNotEnoughData()
        {
            if (!GlobalTestSettings.RunNetworkTests)
            {
                Assert.Inconclusive(GlobalTestSettings.NetworkTestsDisabledMessage);
            }

            int payloadSize = 262144; // 256k

            TokenResponse currentToken = this.GetCurrentToken();

            Guid remoteTestFolderName = Guid.NewGuid();
            Item remoteTestFolder = CreateOneDriveTestDirectory(currentToken, remoteTestFolderName.ToString("D")).Result;

            using (OneDriveClient client = new OneDriveClient(currentToken))
            {
                // Specify the upload size as 16 bytes larger than we are actually going to send.
                OneDriveUploadSession session =
                    client.CreateUploadSession(remoteTestFolder.Id, "uploadTest.txt", payloadSize + 16).Result;

                using (OneDriveFileUploadStream stream = new OneDriveFileUploadStream(client, session))
                {
                    byte[] data = CreateUploadBuffer(payloadSize);

                    // Write the data in 1k chunks
                    int writeSize = 1024;
                    for (int j = 0; j < payloadSize; j += writeSize)
                    {
                        stream.Write(data, j, writeSize);
                    }
                }

                Assert.AreEqual(OneDriveFileUploadState.Cancelled, session.State);
            }
        }

        [TestMethod]
        public void OneDriveEmptyFileUploadTest()
        {
            if (!GlobalTestSettings.RunNetworkTests)
            {
                Assert.Inconclusive(GlobalTestSettings.NetworkTestsDisabledMessage);
            }

            TokenResponse currentToken = this.GetCurrentToken();

            using (OneDriveClient client = new OneDriveClient(currentToken))
            {
                try
                {
                    client.CreateUploadSession("nonExistentId", "uploadTest.txt", 0).Wait();
                }
                catch (AggregateException aggregateException)
                {
                    Assert.IsNotNull(aggregateException.InnerException);
                    Assert.IsInstanceOfType(aggregateException.InnerException, typeof (ArgumentOutOfRangeException));
                }
                
            }
        }

        [TestMethod]
        public void OneDrive1ByteFileUploadTest()
        {
            if (!GlobalTestSettings.RunNetworkTests)
            {
                Assert.Inconclusive(GlobalTestSettings.NetworkTestsDisabledMessage);
            }

            int fragmentSize = 327680; // 320k
            int payloadSize = 1;

            TokenResponse currentToken = this.GetCurrentToken();

            Guid remoteTestFolderName = Guid.NewGuid();
            Item remoteTestFolder = CreateOneDriveTestDirectory(currentToken, remoteTestFolderName.ToString("D")).Result;

            using (OneDriveClient client = new OneDriveClient(currentToken))
            {
                OneDriveUploadSession session =
                    client.CreateUploadSession(remoteTestFolder.Id, "uploadTest.txt", payloadSize).Result;

                using (OneDriveFileUploadStream stream = new OneDriveFileUploadStream(client, session, fragmentSize))
                {
                    // Create a buffer for the data we want to send, and fill it with ASCII text
                    stream.Write(new byte[] { 0xAB }, 0, 1);
                }

                Assert.AreEqual(OneDriveFileUploadState.Completed, session.State);
            }
        }

        [TestMethod]
        public void OneDriveFileDownloadTest()
        {
            if (!GlobalTestSettings.RunNetworkTests)
            {
                Assert.Inconclusive(GlobalTestSettings.NetworkTestsDisabledMessage);
            }

            TokenResponse currentToken = this.GetCurrentToken();

            using (OneDriveClient client = new OneDriveClient(currentToken))
            {
                string path = "Sample%20Pictures/sample_photo_02.jpg";
                Item item = client.GetItemByPathAsync(path).Result;
                Uri downloadUri = client.GetDownloadUriForItem(item.Id).Result;

                using (OneDriveFileDownloadStream stream = new OneDriveFileDownloadStream(client, downloadUri))
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        byte[] buffer = new byte[32768];
                        int totalRead = 0;
                        while (true)
                        {
                            int read = stream.Read(buffer, 0, 32768);
                            totalRead += read;
                            ms.Write(buffer, 0, read);

                            if (read == 0 || read < 32768)
                            {
                                Assert.AreEqual(ms.Position, 76929);
                                Assert.AreEqual(totalRead, 76929);
                                break;
                            }
                        }
                    }
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

            TokenResponse currentToken = this.GetCurrentToken();

            Global.Initialize(testRootPath);
            SyncRelationship newRelationship = SyncRelationship.Create();

            OneDriveAdapter sourceAdapter = new OneDriveAdapter(newRelationship)
            {
                CurrentToken = currentToken,
            };

            sourceAdapter.Configuration.IsOriginator = true;
            sourceAdapter.Config.TargetPath = "OneDrive/SyncTest";
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
            //string foo = "";
            //foreach (EntryUpdateInfo result in run1.AnalyzeResult.EntryResults)
            //{
            //    var path = result.Entry.GetRelativePath(newRelationship, "\\");
            //    foo += "new Tuple<string, long>(\"" + path.Replace("\\", "\\\\") + "\", " + result.Entry.Size + ")," + Environment.NewLine;
            //}


            //Assert.AreEqual(syncFileList.Count, run1.AnalyzeResult.EntryResults.Count);
        }

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

            TokenResponse currentToken = this.GetCurrentToken();

            Global.Initialize(testRootPath);
            SyncRelationship newRelationship = SyncRelationship.Create();

            OneDriveAdapter sourceAdapter = new OneDriveAdapter(newRelationship)
            {
                CurrentToken = currentToken,
            };

            sourceAdapter.Config.TargetPath = "OneDrive/SyncTest";
            sourceAdapter.Config.IsOriginator = true;

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

            AnalyzeJob analyzeJob = new AnalyzeJob(newRelationship);

            analyzeJob.ContinuationJob = new SyncJob(newRelationship, analyzeJob.AnalyzeResult)
            {
                TriggerType = SyncTriggerType.Manual
            };

            analyzeJob.Start();

            SyncJob syncJob = (SyncJob) analyzeJob.WaitForCompletion();

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

        [TestMethod]
        public void BasicSyncUploadOnly()
        {
            if (!GlobalTestSettings.RunNetworkTests)
            {
                Assert.Inconclusive(GlobalTestSettings.NetworkTestsDisabledMessage);
            }

            string testRootPath = Path.Combine(this.TestContext.TestLogsDir, this.TestContext.TestName);
            Directory.CreateDirectory(testRootPath);

            string syncDestinationPath = Path.Combine(testRootPath, "Destination");
            Directory.CreateDirectory(syncDestinationPath);

            TokenResponse currentToken = this.GetCurrentToken();

            Global.Initialize(testRootPath);
            SyncRelationship newRelationship = SyncRelationship.Create();

            OneDriveAdapter sourceAdapter = new OneDriveAdapter(newRelationship)
            {
                CurrentToken = currentToken,
            };

            sourceAdapter.Config.TargetPath = "OneDrive/SyncTest";
            sourceAdapter.Config.IsOriginator = true;

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

            AnalyzeJob analyzeJob = new AnalyzeJob(newRelationship);

            analyzeJob.ContinuationJob = new SyncJob(newRelationship, analyzeJob.AnalyzeResult)
            {
                TriggerType = SyncTriggerType.Manual
            };

            analyzeJob.Start();

            SyncJob syncJob = (SyncJob)analyzeJob.WaitForCompletion();

            Assert.IsTrue(syncJob.HasFinished);

            //Assert.AreEqual(syncFileList.Count, run1.AnalyzeResult.EntryResults.Count);
        }

        private static async Task<Item> CreateOneDriveTestDirectory(TokenResponse currentToken, string name)
        {
            using (OneDriveClient client = new OneDriveClient(currentToken))
            {
                // Get the default drive
                Drive drive = await client.GetDefaultDrive().ConfigureAwait(false);

                // Get the 'Testing' folder under the default drive
                Item testingFolder = await client.GetOrCreateFolderAsync(drive, "Testing");

                // Create the unique folder under the 'Testing' folder
                return await client.CreateFolderAsync(testingFolder, name);
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

        private SyncRelationship SetupRelationship(string testRootPath, string syncSourcePath, Item syncDestinationPath)
        {
            Global.Initialize(testRootPath);
            SyncRelationship newRelationship = SyncRelationship.Create();

            WindowsFileSystemAdapter sourceAdapter = new WindowsFileSystemAdapter(newRelationship);

            sourceAdapter.Config.RootDirectory = syncSourcePath;

            sourceAdapter.Configuration.IsOriginator = true;

            OneDriveAdapter destAdapter = new OneDriveAdapter(newRelationship)
            {
                CurrentToken = this.GetCurrentToken(),
            };

            destAdapter.Config.TargetPath = "OneDrive/Testing/" + syncDestinationPath.Name;
            destAdapter.InitializeClient().Wait();

            newRelationship.Adapters.Add(sourceAdapter);
            newRelationship.Adapters.Add(destAdapter);

            newRelationship.SourceAdapter = sourceAdapter;
            newRelationship.DestinationAdapter = destAdapter;

            newRelationship.Name = "Test Relationship #1";
            newRelationship.Description = "Test Relationship Description #1";

            newRelationship.SaveAsync().Wait();

            foreach (AdapterBase adapter in newRelationship.Adapters)
            {
                adapter.InitializeAsync().Wait();
            }

            return newRelationship;
        }

        private static byte[] CreateUploadBuffer(long size)
        {
            // Create a buffer for the data we want to send, and fill it with ASCII text
            byte[] data = new byte[size];
            for (long i = 0; i < data.Length; i++)
            {
                data[i] = (byte)(41 + (i % 0x4e));
            }

            return data;
        }
    }
}