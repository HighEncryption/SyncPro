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

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            string accountInfoFilePath = Path.Combine(
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
            Assert.AreEqual(hashString, uploadResponse.ContentSha1);
            Assert.AreEqual(filename, uploadResponse.FileName);
        }
    }
}