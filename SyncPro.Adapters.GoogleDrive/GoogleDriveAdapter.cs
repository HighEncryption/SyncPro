namespace SyncPro.Adapters.GoogleDrive
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using SyncPro.Adapters.GoogleDrive.DataModel;
    using SyncPro.Configuration;
    using SyncPro.Data;
    using SyncPro.OAuth;
    using SyncPro.Runtime;

    using Item = SyncPro.Adapters.GoogleDrive.DataModel.Item;

    public class GoogleDriveAdapter : AdapterBase
    {
        public static readonly Guid TargetTypeId = Guid.Parse("64a4cd7e-540a-47e8-bd91-bb7b84efdbb3");

        private GoogleDriveClient googleDriveClient;

        public User UserProfile { get; private set; }

        public GoogleDriveAdapter(SyncRelationship relationship)
            : base(relationship, new GoogleDriveAdapterConfiguration())
        {
        }

        public GoogleDriveAdapter(SyncRelationship relationship, GoogleDriveAdapterConfiguration configuration)
            : base(relationship, configuration)
        {
        }

        #region Saved Properties

        public TokenResponse CurrentToken { get; set; }

        public string TargetItemId { get; set; }

        #endregion

        public override Guid GetTargetTypeId()
        {
            return TargetTypeId;
        }

        public override async Task<SyncEntry> CreateRootEntry()
        {
            Item rootItem = await this.googleDriveClient.GetItemById(this.TargetItemId).ConfigureAwait(false);
            return this.CreateEntry(rootItem, null);
        }

        public override async Task<IAdapterItem> GetRootFolder()
        {
            Item rootItem = await this.googleDriveClient.GetItemById(this.TargetItemId).ConfigureAwait(false);
            return new GoogleDriveAdapterItem(rootItem, null, this);
        }

        public override Task CreateItemAsync(SyncEntry entry)
        {
            throw new NotImplementedException();
        }

        public override Stream GetReadStreamForEntry(SyncEntry entry)
        {
            // Get the adapter entry for the parent entry
            var adapterEntry = entry.AdapterEntries.First(e => e.AdapterId == this.Configuration.Id);

            return new GoogleFileDownloadStream(this.googleDriveClient, adapterEntry.AdapterEntryId);
        }

        public override Stream GetWriteStreamForEntry(SyncEntry entry, long length)
        {
            throw new NotImplementedException();
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
            if (folder == null)
            {
                Item root = this.googleDriveClient.GetItemById("root").Result;
                return new List<IAdapterItem>()
                {
                    new GoogleDriveAdapterItem(root, null, this)
                };
            }

            GoogleDriveAdapterItem adapterItem = folder as GoogleDriveAdapterItem;
            Pre.Assert(adapterItem != null, "adapterItem != null");

            var items = this.googleDriveClient.GetChildItems(adapterItem).Result;
            IEnumerable<GoogleDriveAdapterItem> adapterItems = items.Select(i => new GoogleDriveAdapterItem(i, folder, this));
            return adapterItems;
        }

        public override bool IsEntryUpdated(SyncEntry childEntry, IAdapterItem adapterItem, out EntryUpdateResult result)
        {
            throw new NotImplementedException();
        }

        public override SyncEntry CreateSyncEntryForAdapterItem(IAdapterItem item, SyncEntry parentEntry)
        {
            GoogleDriveAdapterItem adapterItem = item as GoogleDriveAdapterItem;
            Pre.Assert(adapterItem != null, "adapterItem != null");
            Pre.Assert(adapterItem.Item != null, "adapterItem.Item != null");

            return this.CreateEntry(adapterItem.Item, parentEntry);
        }

        public override void FinalizeItemWrite(Stream stream, EntryUpdateInfo updateInfo)
        {
            throw new NotImplementedException();
        }

        public async Task SignIn(AuthenticationResult authenticationResult, string codeVerifier)
        {
            // Use the authentication result to get an access token
            this.CurrentToken = await GoogleDriveClient.GetAccessToken(authenticationResult, codeVerifier).ConfigureAwait(false);

            // Create the OneDrive client, set the token refresh callback to save new tokens.
            await this.InitializeClient().ConfigureAwait(false);
        }

        public async Task InitializeClient()
        {
            this.googleDriveClient = new GoogleDriveClient(this.CurrentToken);
            this.googleDriveClient.TokenRefreshed += (s, e) =>
            {
                //this.PersistedConfiguration.CurrentToken = accessToken;
                this.CurrentToken = e.NewToken;
                this.SaveConfiguration();
            };

            // Get the user profile.
            this.UserProfile = await this.googleDriveClient.GetUserInformation().ConfigureAwait(false);
        }

        private SyncEntry CreateEntry(Item item, SyncEntry parent)
        {
            SyncEntry entry = new SyncEntry
            {
                CreationDateTimeUtc = item.CreatedTime.ToUniversalTime(),
                ModifiedDateTimeUtc = item.ModifiedTime,
                Name = item.Name,
                AdapterEntries = new List<SyncEntryAdapterData>()
            };

            if (parent != null)
            {
                entry.ParentEntry = parent;
                entry.ParentId = parent.Id;
            }

            entry.AdapterEntries.Add(new SyncEntryAdapterData()
            {
                AdapterId = this.Configuration.Id,
                SyncEntry = entry,
                AdapterEntryId = item.Id
            });

            if (item.IsFolder)
            {
                entry.Type = SyncEntryType.Directory;
            }
            else
            {
                entry.Type = SyncEntryType.File;
                entry.SourceSize = item.Size;
                entry.SourceMd5Hash = HexToBytes(item.Md5Checksum);
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

        internal static byte[] ItemIdToUniqueId(string itemId)
        {
            Pre.ThrowIfStringNullOrWhiteSpace(itemId, nameof(itemId));
            return Encoding.ASCII.GetBytes(itemId);
        }

        internal static string UniqueIdToItemId(byte[] uniqueId)
        {
            Pre.ThrowIfArgumentNull(uniqueId, nameof(uniqueId));
            return Encoding.ASCII.GetString(uniqueId);
        }
    }

    public class GoogleDriveAdapterConfiguration : AdapterConfiguration
    {
        public override Guid AdapterTypeId => GoogleDriveAdapter.TargetTypeId;
    }
}