namespace SyncPro.UnitTests
{
    using System;
    using System.IO;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using Newtonsoft.Json;

    using SyncPro.Adapters.GoogleDrive;
    using SyncPro.Adapters.GoogleDrive.DataModel;
    using SyncPro.OAuth;
    using SyncPro.Runtime;

    [TestClass]
    public class GoogleDriveTests : AdapterTestsBase<GoogleDriveAdapter>
    {
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
                "SyncProTesting",
                "GoogleDriveTestingToken.json");

            if (!File.Exists(tokenFilePath))
            {
                throw new FileNotFoundException("Token file was not present at path " + tokenFilePath);
            }

            string tokenContent = File.ReadAllText(tokenFilePath);

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
            GoogleDriveTests.classCurrentToken = token;
        }

        [TestMethod]
        public void BasicSyncLocalToGoogleDrive()
        {
            if (!GlobalTestSettings.RunNetworkTests)
            {
                Assert.Inconclusive(GlobalTestSettings.NetworkTestsDisabledMessage);
            }

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

        protected override GoogleDriveAdapter CreateSourceAdapter_BasicSyncDownloadOnly(SyncRelationship newRelationship)
        {
            TokenResponse currentToken = this.GetCurrentToken();

            GoogleDriveAdapter sourceAdapter = new GoogleDriveAdapter(newRelationship)
            {
                TargetItemId = "0B781VIRHEt3xeGpJN1FxNGVpb28",
                CurrentToken = currentToken,
            };

            sourceAdapter.Configuration.IsOriginator = true;

            sourceAdapter.InitializeClient().Wait();

            return sourceAdapter;
        }

        protected override GoogleDriveAdapter CreateSourceAdapter_BasicAnalyzeOnly(SyncRelationship newRelationship)
        {
            TokenResponse currentToken = this.GetCurrentToken();

            GoogleDriveAdapter sourceAdapter = new GoogleDriveAdapter(newRelationship)
            {
                CurrentToken = currentToken
            };

            sourceAdapter.Configuration.IsOriginator = true;
            sourceAdapter.InitializeClient().Wait();

            return sourceAdapter;
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