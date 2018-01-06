namespace SyncPro.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using Newtonsoft.Json;

    using SyncPro.Adapters.BackblazeB2;
    using SyncPro.Adapters.BackblazeB2.DataModel;

    using File = System.IO.File;

    [TestClass]
    public class BackblazeB2SyncTests
    {
        public const string DefaultBucketName = "syncpro-unit-tests";

        public TestContext TestContext { get; set; }

        private static BackblazeB2AccountInfo accountInfo;

        private static string accountInfoFilePath;

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            accountInfoFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "BackblazeB2AccountInfo.json");

            if (File.Exists(accountInfoFilePath))
            {
                string fileContent = File.ReadAllText(accountInfoFilePath);
                accountInfo = JsonConvert.DeserializeObject<BackblazeB2AccountInfo>(fileContent);

                return;
            }
            
            CredentialResult credentials = CredentialHelper.PromptForCredentials(
                "Enter your Backblaze AccountID (username) and Application Key (password)");

            accountInfo = new BackblazeB2AccountInfo
            {
                AccountId = credentials.Username,
                ApplicationKey = credentials.Password
            };

            using (BackblazeB2Client client = CreateClient())
            {
                IList<Bucket> allBuckets = client.ListBucketsAsync().Result;
                Bucket bucket = allBuckets.FirstOrDefault(b => b.BucketName == DefaultBucketName);

                if (bucket == null)
                {
                    bucket = client.CreateBucket(DefaultBucketName, Constants.BucketTypes.Private).Result;
                }

                accountInfo.BucketId = bucket.BucketId;
            }

            string serializedInfo = JsonConvert.SerializeObject(accountInfo, Formatting.Indented);
            File.WriteAllText(accountInfoFilePath, serializedInfo);
        }

        private static BackblazeB2Client CreateClient()
        {
            BackblazeB2Client client = new BackblazeB2Client(
                accountInfo.AccountId,
                accountInfo.ApplicationKey,
                accountInfo.ConnectionInfo);

            client.ConnectionInfoChanged += (sender, args) =>
            {
                accountInfo.ConnectionInfo = args.ConnectionInfo;
                string serializedInfo = JsonConvert.SerializeObject(accountInfo, Formatting.Indented);
                File.WriteAllText(accountInfoFilePath, serializedInfo);
            };

            client.InitializeAsync().Wait();

            return client;
        }

        [TestMethod]
        public void BasicSyncLocalToB2()
        {
            byte[] data = Encoding.UTF8.GetBytes("Hello World!");
            string hashString;

            using (SHA1Cng sha1 = new SHA1Cng())
            {
                byte[] hashData = sha1.ComputeHash(data);
                hashString = BitConverter.ToString(hashData).Replace("-", "");
            }

            BackblazeB2FileUploadResponse uploadResponse;
            string filename = Guid.NewGuid().ToString("N") + ".txt";

            using (MemoryStream ms = new MemoryStream(data))
            using (BackblazeB2Client client = CreateClient())
            {
                uploadResponse = client.UploadFile(
                    filename,
                    hashString,
                    data.Length,
                    accountInfo.BucketId,
                    ms).Result;
            }

            Assert.AreEqual(accountInfo.BucketId, uploadResponse.BucketId);
            Assert.AreEqual(data.Length, uploadResponse.ContentLength);
            Assert.AreEqual(hashString.ToUpperInvariant(), uploadResponse.ContentSha1.ToUpperInvariant());
            Assert.AreEqual(filename, uploadResponse.FileName);
        }

        [TestMethod]
        public void BasicUploadLargeFile()
        {
            byte[] part1 = new byte[0x500000]; // 5MB
            byte[] part2 = new byte[0x500000]; // 5MB
            byte[] part3 = new byte[0x10000]; // 64K

            FillByteArray(part1);
            FillByteArray(part2);
            FillByteArray(part3);

            string[] sha1Array = new string[3];

            using (SHA1Cng sha1 = new SHA1Cng())
            {
                sha1Array[0] = BitConverter.ToString(sha1.ComputeHash(part1)).Replace("-", "").ToLowerInvariant();
                sha1Array[1] = BitConverter.ToString(sha1.ComputeHash(part2)).Replace("-", "").ToLowerInvariant();
                sha1Array[2] = BitConverter.ToString(sha1.ComputeHash(part3)).Replace("-", "").ToLowerInvariant();
            }

            string filename = Guid.NewGuid().ToString("N") + ".txt";

            using (BackblazeB2Client client = CreateClient())
            {
                ListLargeUnfinishedFilesResponse unfinishedFiles = 
                    client.GetUnfinishedLargeFiles(accountInfo.BucketId).Result;

                StartLargeFileResponse startLargeFileResponse = 
                    client.StartLargeUpload(accountInfo.BucketId, filename).Result;

                GetUploadPartUrlResponse getPartUploadResponse = 
                    client.GetUploadPartUrl(startLargeFileResponse.FileId).Result;

                using (MemoryStream memoryStream = new MemoryStream(part1))
                {
                    UploadPartResponse partUploadResponse =
                        client.UploadPart(
                                getPartUploadResponse.UploadUrl,
                                getPartUploadResponse.AuthorizationToken,
                                1,
                                sha1Array[0],
                                part1.Length,
                                memoryStream)
                            .Result;

                    Assert.AreEqual(5242880, int.Parse(partUploadResponse.ContentLength));
                    Assert.AreEqual(1, Int32.Parse(partUploadResponse.PartNumber));
                    Assert.AreEqual(sha1Array[0], partUploadResponse.ContentSha1);
                }

                using (MemoryStream memoryStream = new MemoryStream(part2))
                {
                    UploadPartResponse partUploadResponse =
                        client.UploadPart(
                                getPartUploadResponse.UploadUrl,
                                getPartUploadResponse.AuthorizationToken,
                                2,
                                sha1Array[1],
                                part2.Length,
                                memoryStream)
                            .Result;

                    Assert.AreEqual(5242880, int.Parse(partUploadResponse.ContentLength));
                    Assert.AreEqual(2, int.Parse(partUploadResponse.PartNumber));
                    Assert.AreEqual(sha1Array[1], partUploadResponse.ContentSha1);
                }

                using (MemoryStream memoryStream = new MemoryStream(part3))
                {
                    UploadPartResponse partUploadResponse =
                        client.UploadPart(
                                getPartUploadResponse.UploadUrl,
                                getPartUploadResponse.AuthorizationToken,
                                3,
                                sha1Array[2],
                                part3.Length,
                                memoryStream)
                            .Result;

                    Assert.AreEqual(65536, int.Parse(partUploadResponse.ContentLength));
                    Assert.AreEqual(3, int.Parse(partUploadResponse.PartNumber));
                    Assert.AreEqual(sha1Array[2], partUploadResponse.ContentSha1);
                }

                FinishLargeFileResponse finishLargeFileResponse =
                    client.FinishLargeFile(
                            startLargeFileResponse.FileId,
                            sha1Array)
                        .Result;

                Assert.AreEqual(accountInfo.BucketId, finishLargeFileResponse.BucketId);
                Assert.AreEqual(10551296, finishLargeFileResponse.ContentLength);
            }
        }

        private static void FillByteArray(byte[] data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)(i % 16);
            }
        }
    }
}