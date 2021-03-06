﻿namespace SyncPro.Adapters.BackblazeB2
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;

    using SyncPro.Adapters.BackblazeB2.DataModel;
    using SyncPro.Data;
    using SyncPro.Runtime;
    using SyncPro.Tracing;

    using File = SyncPro.Adapters.BackblazeB2.DataModel.File;

    public class BackblazeB2Adapter : AdapterBase
    {
        public static readonly Guid TargetTypeId = Guid.Parse("771fbdb2-cbb8-457e-a552-ca1f02c9d707");

        private BackblazeB2Client backblazeClient;

        public BackblazeB2Adapter(SyncRelationship relationship, BackblazeB2AdapterConfiguration configuration) 
            : base(relationship, configuration)
        {
        }

        public BackblazeB2Adapter(SyncRelationship relationship)
            : base(relationship, new BackblazeB2AdapterConfiguration())
        {
        }

        public string AccountId { get; set; }

        public BackblazeB2AdapterConfiguration TypedConfiguration
            => (BackblazeB2AdapterConfiguration) this.Configuration;

        public event EventHandler InitializationComplete;

        public bool IsInitialized { get; private set; }

        public override Guid GetTargetTypeId()
        {
            return TargetTypeId;
        }

        public override AdapterCapabilities Capabilities => AdapterCapabilities.None;

        public override async Task<SyncEntry> CreateRootEntry()
        {
            IList<Bucket> allBuckets = await this.backblazeClient.ListBucketsAsync();
            Bucket bucket = allBuckets.First(b => string.Equals(b.BucketId, this.TypedConfiguration.BucketId));

            return new SyncEntry()
            {
                Name = bucket.BucketName,
                AdapterEntries = new List<SyncEntryAdapterData>(),
                CreationDateTimeUtc = DateTime.MinValue,
                ModifiedDateTimeUtc = DateTime.MinValue
            };
        }

        public override async Task<IAdapterItem> GetRootFolder()
        {
            IList<Bucket> allBuckets = await this.backblazeClient.ListBucketsAsync();
            Bucket bucket = allBuckets.First(b => string.Equals(b.BucketId, this.TypedConfiguration.BucketId));

            return new BackblazeB2AdapterItem(
                bucket.BucketName,
                null,
                this,
                SyncAdapterItemType.Directory,
                bucket.BucketId,
                0,
                DateTime.MinValue,
                DateTime.MinValue)
            {
                IsBucket = true
            };
        }

        public override Task CreateItemAsync(SyncEntry entry)
        {
            return SyncPro.TaskExtensions.CompletedTask;
        }

        public override Stream GetReadStreamForEntry(SyncEntry entry)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get a stream for writing the contents of an item
        /// </summary>
        /// <param name="entry">The entry representing the file to be uploaded</param>
        /// <param name="length">The length of the file to be uploaded</param>
        /// <returns>The <see cref="Stream"/>that the file's content can be written to</returns>
        public override Stream GetWriteStreamForEntry(SyncEntry entry, long length)
        {
            // Create a default session. By itself, the file will be uploaded to Backblaze when the stream
            // containins the session is disposed.
            BackblazeB2UploadSession session = new BackblazeB2UploadSession(entry, length);

            // If the file is large enough, call the method below to get the large file upload information. When
            // these properties are present, the stream will upload the file's parts individually.
            if (length >= this.TypedConfiguration.ConnectionInfo.RecommendedPartSize)
            {
                session.StartLargeFileResponse =
                    this.backblazeClient.StartLargeUpload(
                            this.TypedConfiguration.BucketId,
                            entry.GetRelativePath(null, "/"))
                        .Result;

                session.GetUploadPartUrlResponse =
                    this.backblazeClient.GetUploadPartUrl(session.StartLargeFileResponse.FileId).Result;

                // Return the stream type dedicated to uploading large files (via parts)
                return new BackblazeB2LargeUploadStream(
                    this, 
                    session, 
                    Constants.LimitPartMaximumSize,
                    length);
            }

            // The file size is below the large-file limit, so upload directly
            return new BackblazeB2UploadStream(this, session);
        }

        public async Task<BackblazeB2FileUploadResponse> UploadFileDirect(
            SyncEntry entry, 
            Stream contentStream,
            byte[] sha1Hash)
        {
            long size = entry.GetSize(this.Relationship, SyncEntryPropertyLocation.Destination);

            return await this.backblazeClient.UploadFile(
                    entry.GetRelativePath(null, "/"),
                    BitConverter.ToString(sha1Hash).Replace("-", ""),
                    size,
                    this.TypedConfiguration.BucketId,
                    contentStream)
                .ConfigureAwait(false);
        }

        public async Task<UploadPartResponse> UploadPart(
            BackblazeB2UploadSession session,
            int partNumber,
            string sha1Hash,
            long size,
            Stream contentStream)
        {
            return await this.backblazeClient.UploadPart(
                session.GetUploadPartUrlResponse.UploadUrl,
                session.GetUploadPartUrlResponse.AuthorizationToken,
                partNumber,
                sha1Hash,
                size,
                contentStream)
                .ConfigureAwait(false);
        }

        public override void UpdateItem(EntryUpdateInfo updateInfo, SyncEntryChangedFlags changeFlags)
        {
            if (updateInfo.Entry.Type == SyncEntryType.Directory)
            {
                Logger.Debug("Suppressing UpdateItem() call for Directory in BackblazeB2 adapter");
                return;
            }

            throw new NotImplementedException();
        }

        public override void DeleteItem(SyncEntry entry)
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<IAdapterItem> GetAdapterItems(IAdapterItem folder)
        {
            BackblazeB2AdapterItem adapterItem = folder as BackblazeB2AdapterItem;
            Pre.Assert(adapterItem != null, "adapterItem != null");

            string prefix = string.Empty;

            if (!adapterItem.IsBucket)
            {
                prefix = adapterItem.FullName + "/";
            }

            ConfiguredTaskAwaitable<IList<File>> filesTask = this.backblazeClient.ListFileNamesAsync(
                this.TypedConfiguration.BucketId,
                prefix,
                "")
                .ConfigureAwait(false);

            IList<File> result = filesTask.GetAwaiter().GetResult();
            List<BackblazeB2AdapterItem> childItems = new List<BackblazeB2AdapterItem>();

            foreach (File file in result)
            {
                childItems.Add(
                    new BackblazeB2AdapterItem(
                        file.FullName.Split('/').Last(),
                        folder,
                        this,
                        file.IsFileType ? 
                            SyncAdapterItemType.File :
                            SyncAdapterItemType.Directory,
                        file.FileId,
                        file.ContentLength,
                        file.UploadTimestamp,
                        file.FileInfo?.LastModified ?? file.UploadTimestamp));
            }

            return childItems;
        }

        public override bool IsEntryUpdated(SyncEntry childEntry, IAdapterItem adapterItem, out EntryUpdateResult result)
        {
            throw new NotImplementedException();
        }

        public override SyncEntry CreateSyncEntryForAdapterItem(IAdapterItem item, SyncEntry parentEntry)
        {
            throw new NotImplementedException();
        }

        public async Task<IList<Bucket>> GetBucketsAsync()
        {
            return await this.backblazeClient.ListBucketsAsync().ConfigureAwait(false);
        }

        public async Task InitializeClient()
        {
            this.backblazeClient = new BackblazeB2Client(
                this.TypedConfiguration.AccountId,
                this.TypedConfiguration.ApplicationKey,
                this.TypedConfiguration.ConnectionInfo);

            this.backblazeClient.ConnectionInfoChanged += (s, e) =>
            {
                this.TypedConfiguration.ConnectionInfo = e.ConnectionInfo;
                this.AccountId = e.AccountId;
            };

            // Call the initization method to build the connection info if needed.
            await this.backblazeClient.InitializeAsync().ConfigureAwait(false);

            this.IsInitialized = true;
            this.InitializationComplete?.Invoke(this, new EventArgs());
        }

        public override async Task InitializeAsync()
        {
            try
            {
                await this.InitializeClient().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                Exception selectedException = exception;
                AggregateException aggEx = exception as AggregateException;
                if (aggEx?.InnerExceptions.Count == 1 && aggEx.InnerException != null)
                {
                    selectedException = aggEx.InnerException;
                }

                this.FaultInformation = new BackblazeB2InitializationFault(this, selectedException);
            }
        }

        public override void SaveConfiguration()
        {
            base.SaveConfiguration();
        }

        public override byte[] GetItemHash(HashType hashType, IAdapterItem adapterItem)
        {
            // SHA1 is the only hash supported by Blackblaze
            if (hashType == HashType.SHA1)
            {
                return adapterItem.Sha1Hash;
            }

            return null;
        }

        public override void FinalizeItemWrite(Stream stream, EntryUpdateInfo updateInfo)
        {
            string fileId;

            if (stream is BackblazeB2LargeUploadStream largeUploadStream)
            {
                long size = largeUploadStream.Session.Entry.GetSize(this.Relationship, SyncEntryPropertyLocation.Destination);
                if (largeUploadStream.Session.BytesUploaded != size)
                {
                    // TODO: Cancel the upload?
                    throw new Exception(
                        string.Format(
                            "File size if {0}, but uploaded {1}",
                            size,
                            largeUploadStream.Session.BytesUploaded));
                }

                // Allocate the hash array to contain exactly the expected number of hashes
                string[] partSha1Array = new string[largeUploadStream.Session.CurrentPartNumber];

                for (int i = 1; i < largeUploadStream.Session.CurrentPartNumber; i++)
                {
                    partSha1Array[i] = largeUploadStream.Session.PartHashes[i];
                }

                this.backblazeClient.FinishLargeFile(
                    largeUploadStream.Session.StartLargeFileResponse.FileId,
                    partSha1Array).Wait();

                fileId = largeUploadStream.Session.UploadResponse.FileId;
            }
            else if (stream is BackblazeB2UploadStream uploadStream)
            {
                uploadStream.Flush();
                fileId = uploadStream.Session.UploadResponse.FileId;
            }
            else
            {
                throw new NotImplementedException();
            }

            SyncEntryAdapterData adapterData =
                updateInfo.Entry.AdapterEntries.FirstOrDefault(a => a.AdapterId == this.Configuration.Id);

            if (adapterData == null)
            {
                adapterData = new SyncEntryAdapterData
                {
                    SyncEntry = updateInfo.Entry,
                    AdapterId = this.Configuration.Id
                };

                updateInfo.Entry.AdapterEntries.Add(adapterData);
            }

            adapterData.AdapterEntryId = fileId;
        }

        public async Task<Bucket> CreateBucket(string bucketName, string bucketType)
        {
            return await this.backblazeClient.CreateBucket(bucketName, bucketType);
        }
    }
}
