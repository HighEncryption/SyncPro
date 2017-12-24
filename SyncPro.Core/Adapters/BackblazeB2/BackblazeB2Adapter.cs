namespace SyncPro.Adapters.BackblazeB2
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    using SyncPro.Adapters.BackblazeB2.DataModel;
    using SyncPro.Data;
    using SyncPro.Runtime;

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

            return new BackblazeB2BucketItem()
            {
                Name = bucket.BucketName,
                UniqueId = bucket.BucketId,
                ItemType = SyncAdapterItemType.Directory,
                FullName = bucket.BucketName,
                CreationTimeUtc = DateTime.MinValue,
                ModifiedTimeUtc = DateTime.MinValue,
                Adapter = this
            };
        }

        public override async Task CreateItemAsync(SyncEntry entry)
        {
            await Task.Delay(0);
        }

        public override Stream GetReadStreamForEntry(SyncEntry entry)
        {
            throw new NotImplementedException();
        }

        public override Stream GetWriteStreamForEntry(SyncEntry entry, long length)
        {
            BackblazeB2UploadSession session;

            if (length < this.TypedConfiguration.ConnectionInfo.RecommendedPartSize)
            {
                session = new BackblazeB2UploadSession(entry);
            }
            else
            {
                session = this.backblazeClient.StartLargeUpload(entry).Result;
            }

            return new BackblazeB2UploadStream(this, session);
        }

        public async Task<BackblazeB2FileUploadResponse> UploadFileDirect(SyncEntry entry, Stream contentStream)
        {
            return await this.backblazeClient.UploadFile(
                entry.GetRelativePath(null, "/"),
                BitConverter.ToString(entry.Sha1Hash).Replace("-", ""),
                entry.Size,
                this.TypedConfiguration.BucketId,
                contentStream);
        }

        public override void UpdateItem(EntryUpdateInfo updateInfo, SyncEntryChangedFlags changeFlags)
        {
            throw new NotImplementedException();
        }

        public override void DeleteItem(SyncEntry entry)
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<IAdapterItem> GetAdapterItems(IAdapterItem folder)
        {
            throw new NotImplementedException();
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
                this.SaveConfiguration();
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

        public override void FinalizeItemWrite(Stream stream, EntryUpdateInfo updateInfo)
        {
            BackblazeB2UploadStream uploadStream = (BackblazeB2UploadStream)stream;

            SyncEntryAdapterData adapterData =
                updateInfo.Entry.AdapterEntries.FirstOrDefault(a => a.AdapterId == this.Configuration.Id);

            if (adapterData == null)
            {
                adapterData = new SyncEntryAdapterData
                {
                    SyncEntry = updateInfo.Entry,
                    AdapterId = this.Configuration.Id
                };
            }

            adapterData.AdapterEntryId = uploadStream.Session.UploadResponse.FileId;
        }

        public async Task<Bucket> CreateBucket(string bucketName, string bucketType)
        {
            return await this.backblazeClient.CreateBucket(bucketName, bucketType);
        }
    }
}
