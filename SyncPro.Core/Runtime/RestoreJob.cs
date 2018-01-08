namespace SyncPro.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;

    using SyncPro.Adapters;
    using SyncPro.Data;
    using SyncPro.Tracing;

    public class RestoreJob : JobBase
    {
        private readonly IList<SyncEntry> syncEntries;
        private readonly string restorePath;
        private X509Certificate2 encryptionCertificate;

        public RestoreJob(SyncRelationship relationship, IList<SyncEntry> syncEntries, string restorePath) 
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

            RestoreResult result = new RestoreResult();

            RestoreOnlyWindowsFileSystemAdapterConfiguration adapterConfig = new RestoreOnlyWindowsFileSystemAdapterConfiguration
            {
                RootDirectory = this.restorePath
            };

            RestoreOnlyWindowsFileSystemAdapter destAdapter =
                new RestoreOnlyWindowsFileSystemAdapter(this.Relationship, adapterConfig);

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

            using (var db = this.Relationship.GetDatabase())
            {
                foreach (SyncEntry syncEntry in this.syncEntries)
                {
                    if (this.CancellationToken.IsCancellationRequested)
                    {
                        result.Cancelled = true;
                        return;
                    }

                    RestoreItemResult itemResult = new RestoreItemResult
                    {
                        Entry = syncEntry
                    };

                    string message = string.Empty;

                    try
                    {
                        AdapterBase sourceAdapter = this.Relationship.Adapters.FirstOrDefault(
                            a => a.Configuration.Flags != AdapterFlags.Originator);

                        await this.RestoreFileAsync(
                                syncEntry,
                                sourceAdapter,
                                destAdapter,
                                db)
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
                        Logger.ChangeSynchronzied(
                            Logger.BuildEventMessageWithProperties(
                                message,
                                new Dictionary<string, object>()));
                    }
                }
            }
        }

        private async Task RestoreFileAsync(
            SyncEntry syncEntry,
            AdapterBase fromAdapter,
            AdapterBase toAdapter,
            SyncDatabase db)
        {
            EntryUpdateInfo updateInfo = new EntryUpdateInfo(
                syncEntry,
                fromAdapter, 
                SyncEntryChangedFlags.Restored,
                syncEntry.GetRelativePath(db, "/"));

            FileCopyHelper fileCopyHelper = new FileCopyHelper(
                this.Relationship,
                fromAdapter,
                toAdapter,
                updateInfo,
                null,
                this.encryptionCertificate,
                this.CancellationToken,
                this.CopyProgressChanged);

            Logger.Debug("Creating item with content");
            await fileCopyHelper.CopyFileAsync().ConfigureAwait(false);

        }

        private void CopyProgressChanged(CopyProgressInfo obj)
        {
            throw new NotImplementedException();
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
        public SyncEntry Entry { get; set; }

        public EntryUpdateState State { get; set; }

        public string ErrorMessage { get; set; }
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