namespace SyncPro.Adapters.MicrosoftAzureStorage
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlTypes;
    using System.Diagnostics.Contracts;
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
    using SyncPro.Tracing;

    using TaskExtensions = SyncPro.TaskExtensions;

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
            return TaskExtensions.CompletedTask;
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

            // If there are any block IDs in the block list, then the file was uploaded using blocks (as opposed to
            // uploading the file as a single blob). For this, we need to call PutBlockList to finalize the creation
            // of the blob in storage.
            if (uploadStream.BlockList.Any())
            {
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

            adapterData.AdapterEntryId = GetUniqueIdForFile(updateInfo.RelativePath);
        }

        public override void UpdateItem(EntryUpdateInfo updateInfo, SyncEntryChangedFlags changeFlags)
        {
            if (updateInfo.Entry.Type == SyncEntryType.Directory)
            {
                Logger.Debug("Suppressing UpdateItem() call for Directory in BackblazeB2 adapter");
                return;
            }

            // The changeFlags parameter is passed by value, so we will modify it as we go to unset
            // the properties that we change. If we are left with a changeFlags that is non-zero, 
            // then we missed a case.
            if ((changeFlags & SyncEntryChangedFlags.ModifiedTimestamp) != 0)
            {
                Pre.Assert(updateInfo.ModifiedDateTimeUtcNew != null, "updateInfo.ModifiedDateTimeUtcNew != null");

                // No action needed at the remote end. Updating the blob will update the Last-Modified
                // property. However, we do need to update the Entry.
                updateInfo.Entry.ModifiedDateTimeUtc = updateInfo.ModifiedDateTimeUtcNew.Value;

                changeFlags &= ~SyncEntryChangedFlags.ModifiedTimestamp;
            }

            if (changeFlags != SyncEntryChangedFlags.None)
            {
                throw new NotImplementedException("changeFlags = " + changeFlags);
            }
        }

        public override void DeleteItem(SyncEntry entry)
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<IAdapterItem> GetAdapterItems(IAdapterItem folder)
        {
            // When querying items in the root of the container, folder will have a name of '{containerName}', and when
            // querying items in a folder other than the root, folder will have a have a name of
            // '{containerName}/{folder}'. We will need to reformat this in order to property query storage.
            string prefix = null; 

            // Start by triming off the container name from the front of the folder name
            string relName = folder.FullName.Substring(this.TypedConfiguration.ContainerName.Length);

            // If the folder name is empty, then we are querying the container, so leave the prefix empty.
            if (!string.IsNullOrWhiteSpace(relName))
            {
                prefix = relName;

                // Add a '/' character to the end of the folder name if needed
                if (!prefix.EndsWith(this.PathSeparator))
                {
                    prefix += this.PathSeparator;
                }

                // Ensure that the folder name does NOT start with a '/'.
                prefix = prefix.TrimStart('/');
            }

            ConfiguredTaskAwaitable<IList<ContainerItem>> listBlobsTask = this.storageClient.ListBlobsAsync(
                    this.TypedConfiguration.ContainerName,
                    this.PathSeparator,
                    prefix)
                .ConfigureAwait(false);

            IList<ContainerItem> result = listBlobsTask.GetAwaiter().GetResult();

            foreach (ContainerItem item in result)
            {
                // The item name returned by storage has the prefix at the beginning of the name, which
                // we will need to trim off
                string itemName = prefix != null ? item.Name.Substring(prefix.Length) : item.Name;

                string fullPath = string.Format("{0}/{1}", folder.FullName, itemName);

                string computedId = GetUniqueIdForFile(fullPath);

                BlobPrefix blobPrefix = item as BlobPrefix;
                if (blobPrefix != null)
                {
                    // Azure returns the blob prefix with a '/' character on the end. We need to trim this off
                    // prior to returning it to the caller.
                    yield return new AzureStorageAdapterItem(
                        itemName.TrimEnd('/'),
                        folder,
                        this,
                        SyncAdapterItemType.Directory,
                        computedId,
                        0,
                        DateTime.Now, 
                        DateTime.Now);

                    continue;
                }

                Blob blob = item as Blob;
                Pre.Assert(blob != null, "blob != null");

                yield return new AzureStorageAdapterItem(
                    itemName,
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
                Name = item.Name,
                AdapterEntries = new List<SyncEntryAdapterData>(),
                CreationDateTimeUtc = adapterItem.CreationTimeUtc,
                ModifiedDateTimeUtc = adapterItem.ModifiedTimeUtc
            };

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

        [Pure]
        private string GetUniqueIdForFile(string path)
        {
            using (var sha256 = new SHA256CryptoServiceProvider())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.Unicode.GetBytes(path));
                return Convert.ToBase64String(hashBytes);
            }
        }
    }
}
