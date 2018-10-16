namespace SyncPro.Adapters.MicrosoftAzureStorage
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlTypes;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Runtime.CompilerServices;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;

    using SyncPro.Adapters.MicrosoftAzureStorage.DataModel;
    using SyncPro.Configuration;
    using SyncPro.Data;
    using SyncPro.Runtime;

    public class AzureStorageAdapter : AdapterBase
    {
        public static readonly Guid TargetTypeId = Guid.Parse("3EF1B90E-C5EF-42EE-A441-78A220B7107F");

        private AzureStorageClient storageClient;

        public AzureStorageAdapter(SyncRelationship relationship, AdapterConfiguration configuration) 
            : base(relationship, configuration)
        {
        }

        public AzureStorageAdapter(SyncRelationship relationship)
            : base(relationship, new AzureStorageAdapterConfiguration())
        {
        }

        public AzureStorageAdapterConfiguration TypedConfiguration
            => (AzureStorageAdapterConfiguration) this.Configuration;

        public bool IsInitialized { get; private set; }

        #region AdapterBase Members

        public override Guid GetTargetTypeId()
        {
            return TargetTypeId;
        }

        public override async Task<SyncEntry> CreateRootEntry()
        {
            IList<Container> allContainers = await this.storageClient.ListContainersAsync();
            Container container =
                allContainers.First(c => string.Equals(c.Name, this.TypedConfiguration.ContainerName));

            return new SyncEntry()
            {
                Name = container.Name,
                AdapterEntries = new List<SyncEntryAdapterData>(),
                CreationDateTimeUtc = SqlDateTime.MinValue.Value,
                ModifiedDateTimeUtc = container.LastModified,
                EntryLastUpdatedDateTimeUtc = DateTime.UtcNow,
                Type = SyncEntryType.Directory
            };
        }

        public override async Task<IAdapterItem> GetRootFolder()
        {
            IList<Container> allContainers = await this.storageClient.ListContainersAsync();
            Container container =
                allContainers.First(c => string.Equals(c.Name, this.TypedConfiguration.ContainerName));

            return new AzureStorageAdapterItem(
                container.Name,
                null,
                this,
                SyncAdapterItemType.Directory,
                "/" + container.Name, // TODO: Does this make sense? I cant find anything in the container itself to use
                0,
                DateTime.MinValue,
                DateTime.MinValue)
            {
                IsContainer = true
            };
        }

        public override Task CreateItemAsync(SyncEntry entry)
        {
            throw new NotImplementedException();
        }

        public override Stream GetReadStreamForEntry(SyncEntry entry)
        {
            long size = entry.GetSize(this.Relationship, SyncEntryPropertyLocation.Destination);

            using (SyncDatabase db = this.Relationship.GetDatabase())
            {
                return new AzureStorageDownloadStream(
                    this.storageClient,
                    this.TypedConfiguration.ContainerName,
                    entry.GetRelativePath(db, "/"),
                    size);
            }
        }

        public override Stream GetWriteStreamForEntry(SyncEntry entry, long length)
        {
            long size = entry.GetSize(this.Relationship, SyncEntryPropertyLocation.Source);

            using (SyncDatabase db = this.Relationship.GetDatabase())
            {
                return new AzureStorageUploadStream(
                    this.storageClient,
                    this.TypedConfiguration.ContainerName,
                    entry.GetRelativePath(db, "/"),
                    size);
            }
        }

        public override void FinalizeItemWrite(Stream stream, EntryUpdateInfo updateInfo)
        {
            AzureStorageUploadStream uploadStream = stream as AzureStorageUploadStream;

            Pre.ThrowIfArgumentNull(uploadStream, "uploadStream");

            if (!uploadStream.BlockList.Any())
            {
                // There are not block IDs created, which means that the file was uploaded
                // in a single request.
                return;
            }

            HttpResponseMessage response = this.storageClient.PutBlockListAsync(
                this.TypedConfiguration.ContainerName,
                uploadStream.FileName,
                uploadStream.BlockList).Result;

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    throw new AzureStorageHttpException();
                }
            }
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
            string prefix = null; // = folder.FullName;

            string relName = folder.FullName.Substring(this.TypedConfiguration.ContainerName.Length);
            if (!string.IsNullOrWhiteSpace(relName))
            {
                prefix = relName;

                if (!prefix.EndsWith(this.PathSeparator))
                {
                    prefix += this.PathSeparator;
                }
            }

            ConfiguredTaskAwaitable<IList<Blob>> listBlobsTask = this.storageClient.ListBlobsAsync(
                    this.TypedConfiguration.ContainerName,
                    this.PathSeparator,
                    prefix)
                .ConfigureAwait(false);

            IList<Blob> result = listBlobsTask.GetAwaiter().GetResult();

            foreach (Blob blob in result)
            {
                string fullPath = string.Format("{0}/{1}", folder.FullName, blob.Name);

                string computedId;
                using (var sha256 = new SHA256CryptoServiceProvider())
                {
                    var hashBytes = sha256.ComputeHash(Encoding.Unicode.GetBytes(fullPath));
                    computedId = BitConverter.ToString(hashBytes).Replace("-", "");
                }

                yield return new AzureStorageAdapterItem(
                    blob.Name,
                    folder,
                    this,
                    SyncAdapterItemType.File,
                    computedId,
                    blob.Length,
                    blob.Created,
                    blob.LastModified);
            }
        }

        public override bool IsEntryUpdated(SyncEntry childEntry, IAdapterItem adapterItem, out EntryUpdateResult result)
        {
            throw new NotImplementedException();
        }

        public override SyncEntry CreateSyncEntryForAdapterItem(IAdapterItem item, SyncEntry parentEntry)
        {
            AzureStorageAdapterItem adapterItem = (AzureStorageAdapterItem) item;

            SyncEntry entry = new SyncEntry
            {
                //CreationDateTimeUtc = item.CreatedDateTime.ToUniversalTime(),
                Name = item.Name,
                AdapterEntries = new List<SyncEntryAdapterData>(),
                CreationDateTimeUtc = adapterItem.CreationTimeUtc,
                ModifiedDateTimeUtc = adapterItem.ModifiedTimeUtc
            };

            //if (item.ModifiedTimeUtc.HasValue)
            //{
            //    entry.ModifiedDateTimeUtc = item.LastModifiedDateTime.Value;
            //}

            if (parentEntry != null)
            {
                entry.ParentEntry = parentEntry;
                entry.ParentId = parentEntry.Id;
            }

            entry.AdapterEntries.Add(new SyncEntryAdapterData()
            {
                AdapterId = this.Configuration.Id,
                SyncEntry = entry,
                AdapterEntryId = item.UniqueId
            });

            if (adapterItem.ItemType == SyncAdapterItemType.File)
            {
                entry.Type = SyncEntryType.File;
                entry.SetSize(this.Relationship, SyncEntryPropertyLocation.Source, adapterItem.Size);
                // TODO Set MD5 hash?
            }
            else
            {
                entry.Type = SyncEntryType.Directory;
            }


            if (entry.Type == SyncEntryType.Undefined)
            {
                throw new Exception(string.Format("Unknown type for Item {0} ({1})", item.Name, item.UniqueId));
            }

            // TODO: FIX THIS
            //if (this.Relationship.Configuration.SyncTimestamps)
            //{
            //    entry.CreationDateTimeUtc = info.CreationTimeUtc;
            //    entry.ModifiedDateTimeUtc = info.LastWriteTimeUtc;
            //}

            entry.EntryLastUpdatedDateTimeUtc = DateTime.UtcNow;

            return entry;
        }

        public override byte[] GetItemHash(HashType hashType, IAdapterItem adapterItem)
        {
            throw new NotImplementedException();
        }

        public override Task<byte[]> GetItemThumbnail(string itemId, string relativePath)
        {
            throw new NotImplementedException();
        }

        public override string PathSeparator => "/";

        #endregion

        public void InitializeClient()
        {
            this.storageClient = new AzureStorageClient(
                this.TypedConfiguration.AccountName,
                this.TypedConfiguration.AccountKey);

            this.IsInitialized = true;
        }
    }
}
