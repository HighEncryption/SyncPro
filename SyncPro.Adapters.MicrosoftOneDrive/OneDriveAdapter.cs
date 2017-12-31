namespace SyncPro.Adapters.MicrosoftOneDrive
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using SyncPro.Adapters.MicrosoftOneDrive.DataModel;
    using SyncPro.Data;
    using SyncPro.OAuth;
    using SyncPro.Runtime;
    using SyncPro.Tracing;

    public class OneDriveAdapter : AdapterBase, IChangeTracking, IChangeNotification
    {
        public static readonly Guid TargetTypeId = Guid.Parse("48db1119-1fff-4d97-99ba-2e715a53619a");

        private static readonly TimeSpan OneDriveChangeNotificationPollingInterval
            = TimeSpan.FromSeconds(120);

        private OneDriveClient oneDriveClient;

        public override string PathSeparator => "/";

        public UserProfile UserProfile { get; private set; }

        public event EventHandler InitializationComplete;

        public bool IsInitialized { get; private set; }

        public TokenResponse CurrentToken { get; set; }


        public OneDriveAdapterConfiguration Config => (OneDriveAdapterConfiguration) this.Configuration;

        public OneDriveAdapter(SyncRelationship relationship) 
            : base(relationship, new OneDriveAdapterConfiguration())
        {
        }

        public OneDriveAdapter(SyncRelationship relationship, OneDriveAdapterConfiguration configuration) 
            : base(relationship, configuration)
        {
        }

        public override Guid GetTargetTypeId()
        {
            return TargetTypeId;
        }

        /// <inheritdoc />
        public override async Task<SyncEntry> CreateRootEntry()
        {
            var root = await this.GetRootItemContainer().ConfigureAwait(false);

            if (!root.IsItem)
            {
                return new SyncEntry()
                {
                    Name = OneDriveAdapterItem.DefaultDriveName,
                    AdapterEntries = new List<SyncEntryAdapterData>(),
                    CreationDateTimeUtc = DateTime.MinValue,
                    ModifiedDateTimeUtc = DateTime.MinValue
                };
            }

            //this.Configuration.RootIndexEntry = this.CreateEntry(root.Item, null, false);
            return this.CreateEntry(root.Item, null);
        }

        private SyncEntry CreateEntry(Item item, SyncEntry parent)
        {
            SyncEntry entry = new SyncEntry
            {
                CreationDateTimeUtc = item.CreatedDateTime.ToUniversalTime(),
                Name = item.Name,
                AdapterEntries = new List<SyncEntryAdapterData>()
            };

            if (item.LastModifiedDateTime.HasValue)
            {
                entry.ModifiedDateTimeUtc = item.LastModifiedDateTime.Value;
            }

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

            if (item.File != null)
            {
                entry.Type = SyncEntryType.File;
                entry.SourceSize = item.Size;

                if (item.File.Hashes != null && !string.IsNullOrWhiteSpace(item.File.Hashes.Sha1Hash))
                {
                    entry.SourceSha1Hash = HexToBytes(item.File.Hashes.Sha1Hash);
                }
            }

            if (item.Folder != null)
            {
                entry.Type = SyncEntryType.Directory;
            }

            if (entry.Type == SyncEntryType.Undefined)
            {
                throw new Exception(string.Format("Unknown type for Item {0} ({1})", item.Name, item.Id));
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

        public override async Task InitializeAsync()
        {
            try
            {
                await this.InitializeClient().ConfigureAwait(false);
            }
            catch (OneDriveTokenRefreshFailedException)
            {
                // Indicates that a failure occurred while refreshing the auth token (which is done during
                // initialization
                this.FaultInformation = new OneDriveRefreshTokenExpiredFault(this);
            }
            catch (Exception exception)
            {
                Exception selectedException = exception;
                AggregateException aggEx = exception as AggregateException;
                if (aggEx?.InnerExceptions.Count == 1 && aggEx.InnerException != null)
                {
                    selectedException = aggEx.InnerException;
                }

                this.FaultInformation = new OneDriveInitializationFault(this, selectedException);
            }
        }

        public override async Task<IAdapterItem> GetRootFolder()
        {
            ItemContainer rootItemContainer = await this.GetRootItemContainer().ConfigureAwait(false);

            if (rootItemContainer.IsItem)
            {
                return new OneDriveAdapterItem(rootItemContainer.Item, null, this);
            }

            return new OneDriveAdapterItem(rootItemContainer.Drive, this);
        }

        /// <summary>
        /// Get the <see cref="ItemContainer"/> for the item at the root of the target path. This could
        /// be a Drive object, or a folder.
        /// </summary>
        /// <returns>
        /// The <see cref="ItemContainer"/> for the item at the root of the target path.
        /// </returns>
        private async Task<ItemContainer> GetRootItemContainer()
        {
            var pathParts = this.Config.TargetPath.Split('/');
            if (pathParts.Length == 1 && pathParts.First() == OneDriveAdapterItem.DefaultDriveName)
            {
                // The default drive is the target
                return await this.oneDriveClient.GetDefaultDrive().ConfigureAwait(false);
            }

            string path = string.Join("/", this.Config.TargetPath.Split('/').Skip(1));
            return await this.oneDriveClient.GetItemByPathAsync(path).ConfigureAwait(false);
        }

        public override async Task CreateItemAsync(SyncEntry entry)
        {
            ItemContainer parent;

            // Get the parent entry to the one we will be creating. If that entry's parent ID is null, use the target root 
            // as the place to create the item
            if (entry.ParentEntry.ParentId == null)
            {
                parent = await this.GetRootItemContainer().ConfigureAwait(false);
            }
            else
            {
                var parentEntryData = entry.ParentEntry.AdapterEntries.First(e => e.AdapterId == this.Configuration.Id);
                parent = new Item() { Id = parentEntryData.AdapterEntryId };
            }

            string uniqueId;
            if (entry.Type == SyncEntryType.Directory)
            {
                var newFolder = await this.oneDriveClient.CreateFolderAsync(parent, entry.Name).ConfigureAwait(false);
                uniqueId = newFolder.Id;
            }
            else if (entry.Type == SyncEntryType.File)
            {
                var newFile = await this.oneDriveClient.CreateItem(parent, entry.Name).ConfigureAwait(false);
                uniqueId = newFile.Id;
            }
            else
            {
                throw new NotImplementedException();
            }

            entry.AdapterEntries.Add(new SyncEntryAdapterData()
            {
                SyncEntryId = entry.Id,
                AdapterId = this.Configuration.Id,
                AdapterEntryId = uniqueId
            });
        }

        public override Stream GetReadStreamForEntry(SyncEntry entry)
        {
            // Get the adapter entry for the parent entry
            var adapterEntry = entry.AdapterEntries.First(e => e.AdapterId == this.Configuration.Id);

            Uri downloadUri = this.oneDriveClient.GetDownloadUriForItem(adapterEntry.AdapterEntryId).Result;
            Logger.Debug("Opening READ stream for OneDrive file {0} with Uri {1}", entry.Name, downloadUri);

            return new OneDriveFileDownloadStream(this.oneDriveClient, downloadUri);
        }

        public override Stream GetWriteStreamForEntry(SyncEntry entry, long length)
        {
            // Get the adapter entry for the parent entry
            var parentAdapterEntry = entry.ParentEntry.AdapterEntries.First(e => e.AdapterId == this.Configuration.Id);

            // Create the upload sessiond
            OneDriveUploadSession session =
                this.oneDriveClient.CreateUploadSession(parentAdapterEntry.AdapterEntryId, entry.Name, length).Result;

            return new OneDriveFileUploadStream(this.oneDriveClient, session);
        }

        public override void FinalizeItemWrite(Stream stream, EntryUpdateInfo updateInfo)
        {
            OneDriveFileUploadStream uploadStream = (OneDriveFileUploadStream) stream;

            SyncEntryAdapterData adapterEntry = 
                updateInfo.Entry.AdapterEntries.FirstOrDefault(e => e.AdapterId == this.Config.Id);

            if (adapterEntry == null)
            {
                adapterEntry = new SyncEntryAdapterData()
                {
                    SyncEntry = updateInfo.Entry,
                    AdapterId = this.Config.Id
                };

                updateInfo.Entry.AdapterEntries.Add(adapterEntry);
            }

            adapterEntry.AdapterEntryId = uploadStream.UploadSession.Item.Id;
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
                Drive defaultDrive = this.oneDriveClient.GetDefaultDrive().Result;
                return new List<OneDriveAdapterItem>
                {
                    new OneDriveAdapterItem(defaultDrive, this)
                };
            }

            OneDriveAdapterItem adapterItem = folder as OneDriveAdapterItem;
            Pre.Assert(adapterItem != null, "adapterItem != null");

            IEnumerable<Item> items = this.oneDriveClient.GetChildItems(adapterItem).Result;
            IEnumerable<OneDriveAdapterItem> adapterItems = items.Select(i => new OneDriveAdapterItem(i, folder, this));
            return adapterItems;
        }

        public override bool IsEntryUpdated(SyncEntry childEntry, IAdapterItem adapterItem, out EntryUpdateResult result)
        {
            const long TicksPerMillisecond = 10000;
            const long Epsilon = TicksPerMillisecond * 2;

            OneDriveAdapterItem item = adapterItem as OneDriveAdapterItem;

            if (item == null)
            {
                throw new ArgumentException("The adapter item is not of the correct type.", nameof(adapterItem));
            }

            result = new EntryUpdateResult();

            if (item.Item.LastModifiedDateTime != null &&
                Math.Abs(childEntry.ModifiedDateTimeUtc.Ticks - item.Item.LastModifiedDateTime.Value.Ticks) > Epsilon)
            {
                result.ChangeFlags |= SyncEntryChangedFlags.ModifiedTimestamp;
                result.ModifiedTime = item.Item.LastModifiedDateTime.Value;
            }

            if (Math.Abs(childEntry.CreationDateTimeUtc.Ticks - item.Item.CreatedDateTime.Ticks) > Epsilon)
            {
                result.ChangeFlags |= SyncEntryChangedFlags.CreatedTimestamp;
                result.CreationTime = item.Item.CreatedDateTime;
            }

            SyncEntryType fileType = SyncEntryType.Directory;
            if (item.ItemType == SyncAdapterItemType.File)
            {
                fileType = SyncEntryType.File;

                if (item.Item.Size != childEntry.SourceSize)
                {
                    // Before reporting the size of the item as changed, check the SHA1 hash. If the hash is unchanged, then the 
                    // file is the same as it was before. This is due to a bug in OneDrive where the reported size includes
                    // thumbnails for the file. See https://github.com/OneDrive/onedrive-api-docs/issues/123
                    if (item.Item.File.Hashes != null)
                    {
                        byte[] sha1Hash = HexToBytes(item.Item.File.Hashes.Sha1Hash);
                        if (!sha1Hash.SequenceEqual(childEntry.SourceSha1Hash))
                        {
                            result.ChangeFlags |= SyncEntryChangedFlags.FileSize;
                            result.ChangeFlags |= SyncEntryChangedFlags.Sha1Hash;
                        }
                    }
                }
            }

            if (!string.Equals(item.Item.Name, childEntry.Name, StringComparison.Ordinal))
            {
                result.ChangeFlags |= SyncEntryChangedFlags.Renamed;
            }

            // It is possible that a directory was created over a file that previously existed (with the same name). To 
            // handle this, we need to check if the type changed.
            if (childEntry.Type != fileType)
            {
                // TODO: Handle this
                throw new NotImplementedException();
            }

            return result.ChangeFlags != SyncEntryChangedFlags.None;
        }

        public override SyncEntry CreateSyncEntryForAdapterItem(IAdapterItem item, SyncEntry parentEntry)
        {
            OneDriveAdapterItem oneDriveAdapterItem = item as OneDriveAdapterItem;
            Pre.Assert(oneDriveAdapterItem != null, "oneDriveAdapterItem != null");
            Pre.Assert(oneDriveAdapterItem.Item != null, "oneDriveAdapterItem.Item != null");

            // Is this always true?
            Pre.Assert(parentEntry != null, "parentEntry != null");

            return this.CreateEntry(oneDriveAdapterItem.Item, parentEntry);
        }

        public async Task SignIn(AuthenticationResult authenticationResult)
        {
            // Use the authentication result to get an access token
            this.CurrentToken = await OneDriveClient.GetAccessToken(authenticationResult).ConfigureAwait(false);

            // Create the OneDrive client, set the token refresh callback to save new tokens.
            await this.InitializeClient().ConfigureAwait(false);
        }

        public async Task InitializeClient()
        {
            this.oneDriveClient = new OneDriveClient(this.CurrentToken);
            this.oneDriveClient.TokenRefreshed += (s, e) =>
            {
                this.CurrentToken = e.NewToken;
                this.SaveCurrentTokenToConfiguration();
            };

            // Get the user profile. This will have side effect fo refreshing the token and throw
            // an exception if the refresh token is expired and the user needs to re-sign-in.
            this.UserProfile = await this.oneDriveClient.GetUserProfileAsync().ConfigureAwait(false);

            this.IsInitialized = true;
            this.InitializationComplete?.Invoke(this, new EventArgs());
        }

        public void SaveCurrentTokenToConfiguration()
        {
            if (this.CurrentToken == null)
            {
                return;
            }

            this.Config.CurrentToken = this.CurrentToken.DuplicateToken();
            this.Config.CurrentToken.Protect();
        }

        /// <inheritdoc />
        public override void LoadConfiguration()
        {
            Dictionary<string, object> properties =
                new Dictionary<string, object>
                {
                    { "TargetPath", this.Config.TargetPath },
                    { "LatestDeltaToken", this.Config.LatestDeltaToken }
                };

            // The token is normally encrypted in the Configuration object. Because we need to have it 
            // in memory and unencrypted to use it, we keep a second (unencrypted) copy of the token
            // locally in the adapter.
            if (this.Config.CurrentToken != null)
            {
                this.CurrentToken = this.Config.CurrentToken.DuplicateToken();
                this.CurrentToken.Unprotect();

                properties.Add(
                    "CurrentToken",
                    string.Format(
                        "AcquireTime={0}, AccessTokenHash={1}, RefreshTokenHash={2}",
                        this.CurrentToken.AcquireTime,
                        this.CurrentToken.GetAccessTokenHash(),
                        this.CurrentToken.GetRefreshTokenHash()));
            }
            else
            {
                properties.Add("CurrentToken", "(null)");
            }

            Logger.AdapterLoaded(
                "OneDriveAdapter",
                OneDriveAdapter.TargetTypeId,
                properties);
        }

        /// <inheritdoc />
        public override void SaveConfiguration()
        {
            this.SaveCurrentTokenToConfiguration();
        }

        internal static string UniqueIdToItemId(byte[] uniqueId)
        {
            Pre.ThrowIfArgumentNull(uniqueId, nameof(uniqueId));
            return Encoding.ASCII.GetString(uniqueId);
        }

        public async Task<TrackedChange> GetChangesAsync()
        {
            if (!this.Config.TargetPath.StartsWith("OneDrive", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Expected TargetPath to start with 'OneDrive/'");
            }

            // TargetPath has the form OneDrive/path/to/folder. We need the actual path /path/to/folder, so strip off the
            // first 8 characters of the path.
            string rootPath = this.Config.TargetPath.Substring(8);

            OneDriveDeltaView deltaView;
            try
            {
                deltaView = await this.oneDriveClient.GetDeltaView(rootPath, this.Config.LatestDeltaToken).ConfigureAwait(false);
            }
            catch (OneDriveHttpException httpException)
                when (httpException.ErrorResponse != null && httpException.ErrorResponse.Code == "resyncRequired")
            {
                Logger.Warning("Change tracking for adapter {0} failed and a full resync is required.", this.Configuration.Id);

                // If the delta token is stale (or possibly some other condition), the service may reject the delta token and
                // require that a resync be performed using a new delta view. When this occurs, the Location header of the
                // response will contain the new link for acquiring the new delta view.
                KeyValuePair<string, IList<string>> locationHeader = httpException.ResponseHeaders.FirstOrDefault(
                    h => string.Equals(h.Key, "Location", StringComparison.OrdinalIgnoreCase));

                // Verify that the location header is present
                if (string.IsNullOrEmpty(locationHeader.Key) || !locationHeader.Value.Any())
                {
                    throw new OneDriveHttpException("A location was not provided by the service during a delta token refresh.",
                        httpException);
                }

                // Get the new delta view
                try
                {
                    deltaView = await this.oneDriveClient.GetDeltaView(locationHeader.Value.First()).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    throw new OneDriveHttpException("Exception thrown during a delta token refresh.", exception);
                }
            }

            Logger.Debug(
                "OneDriveAdapter successfully retrieved tracked change {0} with {1} changes.",
                deltaView.Token,
                deltaView.Items.Count);

            TrackedChange trackedChange = new TrackedChange(deltaView.Token);

            // Per https://dev.onedrive.com/items/view_delta.htm
            // The same item may appear more than once in a delta feed, for various reasons. You should use the last 
            // occurrence you see.
            foreach (Item item in deltaView.Items)
            {
                var existingItemIndex = trackedChange.Changes.FindIndex(c => c.UniqueId == item.Id);
                if (existingItemIndex > -1)
                {
                    trackedChange.Changes[existingItemIndex] = new OneDriveAdapterItem(item, null, this);
                }
                else
                {
                    trackedChange.Changes.Add(new OneDriveAdapterItem(item, null, this));
                }
            }

            return trackedChange;
        }

        public async Task CommitChangesAsync(TrackedChange trackedChange)
        {
            if (this.Config.LatestDeltaToken == trackedChange.State)
            {
                Logger.Debug("Delta token is already up to date");
                return;
            }

            Logger.Debug("OneDriveAdapter committing tracked change " + trackedChange.State);
            this.Config.LatestDeltaToken = trackedChange.State;

            await this.Relationship.SaveAsync().ConfigureAwait(false);
        }

        public bool HasTrackedState => !string.IsNullOrWhiteSpace(this.Config.LatestDeltaToken);

        #region IChangeNotification Implementation

        public event EventHandler<ItemChangedEventArgs> ItemChanged;

        public bool IsChangeNotificationEnabled { get; private set; }

        public void EnableChangeNotification(bool enabled)
        {
            if (enabled == this.IsChangeNotificationEnabled)
            {
                // Change notification is already enabled/disabled
                return;
            }

            Logger.Info("{0} change notification on adapter {1} for relationship {2}",
                enabled ? "Enabling" : "Disabling",
                this.Configuration.Id,
                this.Relationship.Configuration.RelationshipId);

            if (!enabled)
            {
                this.changeNotificationCancellationTokenSource.Cancel();
                if (!this.changeNotificationTask.Wait(5000))
                {
                    throw new TimeoutException("The change notification task did not terminate in the time allowed.");
                }

                this.IsChangeNotificationEnabled = false;
                return;
            }

            this.changeNotificationCancellationTokenSource = new CancellationTokenSource();
            this.changeNotificationTask = Task.Run(
                this.ChangeNotificationThreadMain,
                this.changeNotificationCancellationTokenSource.Token);

            this.IsChangeNotificationEnabled = true;
        }

        public DateTime GetNextNotificationTime()
        {
            return this.nextNotificationPollTime;
        }

        private CancellationTokenSource changeNotificationCancellationTokenSource;

        private Task changeNotificationTask;

        // The DateTime of the next time when OneDrive will be polled for changes.
        private DateTime nextNotificationPollTime;

        private async Task ChangeNotificationThreadMain()
        {
            while (!this.changeNotificationCancellationTokenSource.IsCancellationRequested)
            {
                TrackedChange changes = null;
                try
                {
                    changes = await this.GetChangesAsync().ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    Logger.LogException(exception, "Failed to pull changes from OneDrive");
                }

                if (changes != null &&
                    !string.Equals(changes.State, this.Config.LatestDeltaToken, StringComparison.Ordinal))
                {
                    this.ItemChanged?.Invoke(this, new ItemChangedEventArgs());
                }

                Logger.Debug("OneDriveAdapter.ChangeNotificationThreadMain delay for " + OneDriveChangeNotificationPollingInterval);

                this.nextNotificationPollTime = DateTime.Now.Add(
                    OneDriveChangeNotificationPollingInterval);

                await Task.Delay(
                        OneDriveChangeNotificationPollingInterval,
                        this.changeNotificationCancellationTokenSource.Token)
                    .ConfigureAwait(false);
            }
        }

        #endregion
    }

    public enum OneDriveItemStreamMode
    {
        Undefined,
        Read,
        Write
    }

    public class OneDriveInitializationFault : AdapterFaultInformation
    {
        public OneDriveAdapter Adapter { get; }
        public Exception Exception { get; }

        public OneDriveInitializationFault(OneDriveAdapter adapter, Exception exception)
        {
            this.Adapter = adapter;
            this.Exception = exception;
        }
    }

    public class OneDriveRefreshTokenExpiredFault : AdapterFaultInformation
    {
        public OneDriveAdapter Adapter { get; }

        public OneDriveRefreshTokenExpiredFault(OneDriveAdapter adapter)
        {
            this.Adapter = adapter;
        }
    }
}
