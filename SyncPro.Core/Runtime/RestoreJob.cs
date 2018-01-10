namespace SyncPro.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;

    using SyncPro.Adapters;
    using SyncPro.Configuration;
    using SyncPro.Data;
    using SyncPro.Tracing;

    public class RestoreJob : JobBase
    {
        private readonly List<SyncEntry> syncEntries;
        private readonly string restorePath;

        private readonly Queue<Tuple<DateTime, long>> throughputCalculationCache = new Queue<Tuple<DateTime, long>>();
        private readonly object progressLock = new object();

        private X509Certificate2 encryptionCertificate;
        private long filesCompleted;
        private long bytesCompleted;

        public event EventHandler<RestoreJobProgressInfo> ProgressChanged;

        public RestoreJob(SyncRelationship relationship, List<SyncEntry> syncEntries, string restorePath) 
            : base(relationship)
        {
            Pre.ThrowIfArgumentNull(relationship, nameof(relationship));
            Pre.ThrowIfArgumentNull(syncEntries, nameof(syncEntries));

            Pre.ThrowIfStringNullOrWhiteSpace(restorePath, nameof(restorePath));

            this.syncEntries = syncEntries;
            this.restorePath = restorePath;
        }

        protected override async Task ExecuteTask()
        {
            if (!this.syncEntries.Any())
            {
                return;
            }

            RestoreOnlyWindowsFileSystemAdapterConfiguration adapterConfig = new RestoreOnlyWindowsFileSystemAdapterConfiguration
            {
                RootDirectory = this.restorePath
            };

            RestoreOnlyWindowsFileSystemAdapter destAdapter =
                new RestoreOnlyWindowsFileSystemAdapter(this.Relationship, adapterConfig);

            List<EntryUpdateInfo> updatesToRestore = new List<EntryUpdateInfo>();

            using (var db = this.Relationship.GetDatabase())
            {
                foreach (SyncEntry syncEntry in this.syncEntries)
                {
                    this.AddSyncEntriesRecursive(db, updatesToRestore, syncEntry, destAdapter);
                }
            }

            updatesToRestore.Sort(new EntryProcessingSorter());

            RestoreResult result = new RestoreResult();

            if (this.Relationship.EncryptionMode != Configuration.EncryptionMode.None)
            {
                X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadOnly);

                var cert = store.Certificates.Find(
                    X509FindType.FindByThumbprint,
                    this.Relationship.EncryptionCertificateThumbprint,
                    false);

                this.encryptionCertificate = cert[0];
            }

            this.filesCompleted = 0;
            this.bytesCompleted = 0;

            foreach (EntryUpdateInfo entryUpdateInfo in updatesToRestore)
            {
                if (this.CancellationToken.IsCancellationRequested)
                {
                    result.Cancelled = true;
                    return;
                }

                RestoreItemResult itemResult = new RestoreItemResult(entryUpdateInfo);
                string message = string.Empty;

                try
                {
                    AdapterBase sourceAdapter = this.Relationship.Adapters.FirstOrDefault(
                        a => a.Configuration.Flags != AdapterFlags.Originator);

                    await this.RestoreFileAsync(
                            entryUpdateInfo,
                            sourceAdapter,
                            destAdapter)
                        .ConfigureAwait(false);

                    message = "The change was successfully synchronized";
                }
                catch (TaskCanceledException)
                {
                    result.Cancelled = true;
                    Logger.Warning("Processing was cancelled");

                    message = "The change was cancelled during processing";
                    itemResult.ErrorMessage = "Processing was cancelled";
                    itemResult.State = EntryUpdateState.Failed;
                }
                catch (Exception exception)
                {
                    Logger.Warning(
                        "Processing failed with {0}: {1}",
                        exception.GetType().FullName,
                        exception.Message);

                    message = "An error occurred while synchronzing the changed.";
                    itemResult.ErrorMessage = exception.Message;
                    itemResult.State = EntryUpdateState.Failed;
                }
                finally
                {
                    Interlocked.Increment(ref this.filesCompleted);

                    Logger.ChangeSynchronzied(
                        Logger.BuildEventMessageWithProperties(
                            message,
                            new Dictionary<string, object>()));
                }
            }
        }

        private void AddSyncEntriesRecursive(
            SyncDatabase db,
            List<EntryUpdateInfo> updatesToRestore,
            SyncEntry syncEntry,
            RestoreOnlyWindowsFileSystemAdapter destAdapter)
        {
            EntryUpdateInfo updateInfo = new EntryUpdateInfo(
                syncEntry,
                destAdapter,
                SyncEntryChangedFlags.Restored,
                syncEntry.GetRelativePath(db, "/"));

            updatesToRestore.Add(updateInfo);

            if (syncEntry.Type == SyncEntryType.Directory)
            {
                List<SyncEntry> childEntries = db.Entries.Where(e => e.ParentId == syncEntry.Id).ToList();
                foreach (SyncEntry childEntry in childEntries)
                {
                    this.AddSyncEntriesRecursive(db, updatesToRestore, childEntry, destAdapter);
                }
            }
        }

        private async Task RestoreFileAsync(
            EntryUpdateInfo updateInfo,
            AdapterBase fromAdapter,
            AdapterBase toAdapter)
        {
            if (updateInfo.Entry.Type == SyncEntryType.Directory)
            {
                await toAdapter.CreateItemAsync(updateInfo.Entry).ConfigureAwait(false);
                return;
            }

            FileCopyHelper fileCopyHelper = new FileCopyHelper(
                this.Relationship,
                fromAdapter,
                toAdapter,
                updateInfo,
                null,
                this.encryptionCertificate,
                this.CancellationToken,
                this.CopyProgressChanged);

            if (this.Relationship.EncryptionMode == EncryptionMode.Encrypt)
            {
                fileCopyHelper.EncryptionMode = EncryptionMode.Decrypt;
            }

            fileCopyHelper.UpdateSyncEntry = false;

            Logger.Debug("Creating item with content");
            await fileCopyHelper.CopyFileAsync().ConfigureAwait(false);
        }

        private void CopyProgressChanged(CopyProgressInfo obj)
        {
            lock (this.progressLock)
            {
                this.bytesCompleted += obj.BytesCopied;

                this.throughputCalculationCache.Enqueue(
                    new Tuple<DateTime, long>(
                        DateTime.Now,
                        this.bytesCompleted));

                int bytesPerSecond = 0;
                if (this.throughputCalculationCache.Count() > 10)
                {
                    Tuple<DateTime, long> oldest = this.throughputCalculationCache.Dequeue();

                    TimeSpan delay = DateTime.Now - oldest.Item1;
                    long bytes = this.bytesCompleted - oldest.Item2;

                    bytesPerSecond = Convert.ToInt32(Math.Floor(bytes / delay.TotalSeconds));
                }

                this.ProgressChanged?.Invoke(
                    this,
                    new RestoreJobProgressInfo(
                        this.FilesTotal,
                        Convert.ToInt32(Interlocked.Read(ref this.filesCompleted)),
                        this.BytesTotal,
                        this.bytesCompleted,
                        bytesPerSecond));
            }
        }
    }

    public class RestoreResult
    {
        public List<RestoreItemResult> ItemResults { get; }

        public bool Cancelled { get; set; }

        public RestoreResult()
        {
            this.ItemResults = new List<RestoreItemResult>();
        }
    }

    public class RestoreItemResult
    {
        public EntryUpdateInfo EntryUpdateInfo { get; }

        public EntryUpdateState State { get; set; }

        public string ErrorMessage { get; set; }

        public RestoreItemResult(EntryUpdateInfo info)
        {
            this.EntryUpdateInfo = info;
        }
    }

    internal class CopyProgressInfo
    {
        public CopyProgressInfo(long bytesCopied, EntryUpdateInfo entryUpdateInfo)
        {
            this.BytesCopied = bytesCopied;
            this.EntryUpdateInfo = entryUpdateInfo;
        }

        public long BytesCopied { get; set; }

        public EntryUpdateInfo EntryUpdateInfo { get; set; }
    }
}