namespace SyncPro.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;

    using JsonLog;

    using SyncPro.Adapters;
    using SyncPro.Data;

    /// <summary>
    /// Enumeration of the options for the result of a sync run
    /// </summary>
    public enum SyncRunResult
    {
        Undefined,
        Success,
        Warning,
        Error,
        NotRun,
        Cancelled,
    }

    /// <summary>
    /// Enumeration of the stages of a sync run
    /// </summary>
    public enum SyncRunStage
    {
        Undefined,
        Analyze,
        Sync
    }

    public class SyncRun
    {
        private readonly SyncRelationship relationship;

        public int Id { get; set; }

        public DateTime StartTime { get; private set; }
        public DateTime? EndTime { get; private set; }

        public AnalyzeRelationshipResult AnalyzeResult { get; set; }

        public bool HasStarted => this.StartTime != DateTime.MinValue;

        public bool HasFinished => this.StartTime != DateTime.MinValue;

        public int FilesTotal { get; private set; }

        private int filesCompleted;

        private int? syncHistoryId;

        public long BytesTotal { get; private set; }

        private long bytesCompleted;

        private Task syncTask;

        private CancellationTokenSource cancellationTokenSource;

        /// <summary>
        /// The result of the sync run
        /// </summary>
        public SyncRunResult SyncResult { get; private set; }

        // Buffer size is 64k
        private const int transferBufferSize = 0x10000;

        public bool AnalyzeOnly
        {
            get { return this.analyzeOnly; }
            set
            {
                if (this.HasStarted)
                {
                    throw new InvalidOperationException("The sync has already started.");
                }

                this.analyzeOnly = value;
            }
        }

        public event EventHandler<SyncRunProgressInfo> ProgressChanged;

        public event EventHandler SyncStarted;

        public event EventHandler SyncFinished;

        private readonly Queue<Tuple<DateTime, long>> throughputCalculationCache = new Queue<Tuple<DateTime, long>>();

        private void RaiseSyncProgressChanged(EntryUpdateInfo updateInfo)
        {
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
                new SyncRunProgressInfo(
                    updateInfo, 
                    this.FilesTotal, 
                    this.filesCompleted, 
                    this.BytesTotal,
                    this.bytesCompleted,
                    bytesPerSecond));
        }

        public SyncRun(SyncRelationship relationship)
        {
            this.relationship = relationship;
        }

        private Stopwatch syncProgressUpdateStopwatch;

        public void Start(SyncTriggerType triggerType)
        {
            if (this.AnalyzeOnly)
            {
                // If this is an analyze-only run, clear out any pre-existing results
                this.AnalyzeResult = null;
            }

            this.cancellationTokenSource = new CancellationTokenSource();
            this.TriggerType = triggerType;

            this.syncTask = Task.Run(this.RunMainThread, this.cancellationTokenSource.Token);
            this.syncTask.ContinueWith(this.MainThreadComplete).ConfigureAwait(false);
        }

        private bool analyzeOnly;

        private async Task RunMainThread()
        {
            this.StartTime = DateTime.Now;
            this.relationship.State = SyncRelationshipState.Running;

            // Create a new sync history entry (not for analyze-only runs)
            this.CreateNewSyncHistoryRun();

            // Raise event that the sync has started
            this.SyncStarted?.Invoke(this, new EventArgs());

            // If there is no analyze result, then this is a new sync run. Create the SyncAnalyzer to process
            // the entries that need to be synchronzied.
            if (this.AnalyzeResult == null)
            {
                SyncAnalyzer syncAnalyzer = new SyncAnalyzer(this.relationship, this.cancellationTokenSource.Token);

                syncAnalyzer.ChangeDetected += (sender, info) =>
                {
                    this.ProgressChanged?.Invoke(this, info);
                };

                this.AnalyzeResult = await syncAnalyzer.AnalyzeChangesAsync().ConfigureAwait(false);
            }

            // If this is an analyze-only run, or if there is nothing to synchronize, dont attempt to synchronize
            if (this.AnalyzeOnly || this.AnalyzeResult.IsUpToDate)
            {
                this.EndTime = DateTime.Now;
                this.SyncResult = SyncRunResult.NotRun;
            }
            else
            {
                // Run actual synchronization of entries
                await this.SyncInternalAsync().ConfigureAwait(false);
            }

            /*
             * The sync run is now complete (if run) or it was not run.
             */

            // If sync was run successfully commit the tracked changes for each adapter
            if (this.SyncResult == SyncRunResult.Success)
            {
                await this.AnalyzeResult.CommitTrackedChangesAsync().ConfigureAwait(false);
            }

            // If a sync run was requested but not needed because all of the files are already up to date, commit change
            // if needed (because the delta token was refreshed).
            if (!this.AnalyzeOnly && this.AnalyzeResult.IsUpToDate)
            {
                await this.AnalyzeResult.CommitTrackedChangesAsync().ConfigureAwait(false);
            }

            // If this is an AnalyzeOnly run and no changes were found, commit tracked changes (if needed). In a case 
            // where the delta token has expired but no changes were made, we can save the new delta token.
            if (this.AnalyzeOnly && this.SyncResult == SyncRunResult.NotRun && this.AnalyzeResult.IsUpToDate)
            {
                await this.AnalyzeResult.CommitTrackedChangesAsync().ConfigureAwait(false);
            }

            this.SaveSyncRunHistory();

            this.SyncFinished?.Invoke(this, new EventArgs());
        }

        private void SaveSyncRunHistory()
        {
            if (this.AnalyzeOnly)
            {
                return;
            }

            using (var db = this.relationship.GetDatabase())
            {
                SyncHistoryData historyData = db.History.First(h => h.Id == this.syncHistoryId);

                historyData.End = this.EndTime;
                historyData.TotalFiles = this.FilesTotal;
                historyData.TotalBytes = this.BytesTotal;
                historyData.Result = this.SyncResult;

                db.SaveChanges();
            }
        }

        private void CreateNewSyncHistoryRun()
        {
            // If this is a full sync run (not just an analyze run), create a history entry. We dont create history
            // entries for analyze-only run.
            if (this.AnalyzeOnly)
            {
                return;
            }

            // Create a history entry for this run in the database
            using (var db = this.relationship.GetDatabase())
            {
                SyncHistoryData syncHistory = new SyncHistoryData()
                {
                    Start = this.StartTime,
                    End = this.EndTime,
                    Result = this.SyncResult,
                    TriggeredBy = this.TriggerType,
                };

                db.History.Add(syncHistory);
                db.SaveChanges();
                this.syncHistoryId = syncHistory.Id;
            }
        }

        private void MainThreadComplete(Task task)
        {
            this.relationship.State = SyncRelationshipState.Idle;
        }

        public SyncTriggerType TriggerType { get; private set; }

        /// <summary>
        /// Asynchronously cancel the sync run
        /// </summary>
        public void Cancel()
        {
            this.cancellationTokenSource.Cancel();
        }

        /// <summary>
        /// Synchronzied changes previously determined by the SyncAnalyzer.
        /// </summary>
        /// <returns>Nothing (async task)</returns>
        private async Task SyncInternalAsync()
        {
            Logger.Info("--------------------------------------------");
            Logger.Info("Beginning SyncInternal()");

            // Get the list of EntryUpdateInfo object for each change that needs to be synchronzied. Be sure that we are
            // gathering the results from each adapter, which is needed in bidirectional sync.
            List<EntryUpdateInfo> updateList = this.AnalyzeResult.AdapterResults.SelectMany(r => r.Value.EntryResults).ToList();

            // Process all of the add/update updates first, then deletes afterward. Deleted need to be done in reverse order, so we will 
            // order them before adding them back to the master list. Start by creating a list of all of the delete operations.
            List<EntryUpdateInfo> deleteList = updateList.Where(e => e.Flags.HasFlag(SyncEntryChangedFlags.Deleted)).ToList();

            // Remove the delete operations from the master list.
            foreach (EntryUpdateInfo entry in deleteList)
            {
                updateList.Remove(entry);
            }

            // Sort the delete list in reverse alpha order. This *should* put deletes of children ahead of parents.
            deleteList.Sort((e1, e2) => string.Compare(e2.RelativePath, e1.RelativePath, StringComparison.Ordinal));

            // Add the sorted deletes to the end of the update list.
            updateList.AddRange(deleteList);

            this.FilesTotal = updateList.Count;
            this.filesCompleted = 0;

            this.BytesTotal = updateList
                .Where(item => item.HasSyncEntryFlag(SyncEntryChangedFlags.IsNewOrUpdated))
                .Aggregate((long)0, (current, info) => current + info.Entry.Size);
            this.bytesCompleted = 0;

            this.syncProgressUpdateStopwatch = new Stopwatch();
            this.syncProgressUpdateStopwatch.Start();

            ThrottlingManager throttlingManager = null;
            if (this.relationship.IsThrottlingEnabled)
            {
                Logger.Debug(
                    "SyncRun will use throttling manager with rate={0} B/sec",
                    this.relationship.IsThrottlingEnabled);

                int bytesPerSecond = this.relationship.ThrottlingValue * this.relationship.ThrottlingScaleFactor;
                throttlingManager = new ThrottlingManager(bytesPerSecond, bytesPerSecond * 3);
            }

            bool syncWasCancelled = false;

            using(throttlingManager)
            using (var db = this.relationship.GetDatabase())
            {
                foreach (EntryUpdateInfo entryUpdateInfo in updateList)
                {
                    if (this.cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        syncWasCancelled = true;
                        break;
                    }

                    if (entryUpdateInfo.State == EntryUpdateState.Succeeded)
                    {
                        Logger.Info(
                            "Skipping synchronization for already-synchronized entry {0} ({1})",
                            entryUpdateInfo.Entry.Id,
                            entryUpdateInfo.Entry.Name);

                        continue;
                    }

                    Logger.Info(
                        "Processing update for entry {0} ({1}) with flags {2}",
                        entryUpdateInfo.Entry.Id,
                        entryUpdateInfo.Entry.Name,
                        string.Join(",", StringExtensions.GetSetFlagNames<SyncEntryChangedFlags>(entryUpdateInfo.Flags)));

                    Pre.Assert(entryUpdateInfo.Flags != SyncEntryChangedFlags.None, "entryUpdateInfo.Flags != None");
                    bool fileProcessed = false;

                    // Find the adapter that this change should be synchronized to. This will be the 
                    // adapter that is not the adapter originates from.
                    // Dev Note: This is currently designed to only handle two adapters (one source and
                    // one destination). When support is added to support more than 2 adapters in a 
                    // relationship, the below code will need to be changed to call ProcessEntryAsync
                    // for each adapter that the change needs to be synchronized to.
                    AdapterBase adapter = this.relationship.Adapters.FirstOrDefault(
                        a => a.Configuration.Id != entryUpdateInfo.OriginatingAdapter.Configuration.Id);
                    Pre.Assert(adapter != null, "adapter != null");

                    try
                    {
                        fileProcessed = await this.ProcessEntryAsync(
                                entryUpdateInfo,
                                adapter,
                                throttlingManager,
                                db)
                            .ConfigureAwait(false);
                    }
                    catch (TaskCanceledException)
                    {
                        Logger.Warning("Processing was cancelled");

                        entryUpdateInfo.ErrorMessage = "Processing was cancelled";
                        entryUpdateInfo.State = EntryUpdateState.Failed;
                    }
                    catch (Exception exception)
                    {
                        Logger.Warning(
                            "Processing failed with {0}: {1}", 
                            exception.GetType().FullName,
                            exception.Message);

                        entryUpdateInfo.ErrorMessage = exception.Message;
                        entryUpdateInfo.State = EntryUpdateState.Failed;
                    }

                    if (fileProcessed)
                    {
                        this.filesCompleted++;

                        SyncHistoryEntryData historyEntry = entryUpdateInfo.CreateSyncHistoryEntryData();

                        Pre.Assert(this.syncHistoryId != null, "this.syncHistoryId != null");

                        historyEntry.SyncHistoryId = this.syncHistoryId.Value;
                        historyEntry.SyncEntryId = entryUpdateInfo.Entry.Id;

                        db.HistoryEntries.Add(historyEntry);
                        db.SaveChanges();
                    }
                }
            }

            // Invoke the ProgressChanged event one final time with a null EntryUpdateInfo object to flush
            // out the final values for files and bytes.
            this.RaiseSyncProgressChanged(null);

            this.EndTime = DateTime.UtcNow;

            if (syncWasCancelled)
            {
                this.SyncResult = SyncRunResult.Cancelled;
            }
            else if (updateList.Any(e => e.State == EntryUpdateState.Failed))
            {
                this.SyncResult = SyncRunResult.Error;
            }
            else
            {
                this.SyncResult = SyncRunResult.Success;
            }

            using (var db = this.relationship.GetDatabase())
            {
                var historyData = db.History.First(h => h.Id == this.syncHistoryId);

                historyData.End = this.EndTime;
                historyData.TotalFiles = this.filesCompleted;
                historyData.TotalBytes = this.bytesCompleted;
                historyData.Result = this.SyncResult;

                db.SaveChanges();
            }
        }

        private async Task<bool> ProcessEntryAsync(
            EntryUpdateInfo entryUpdateInfo,
            AdapterBase adapter, // TODO: Rename to destinationAdapter
            ThrottlingManager throttlingManager,
            SyncDatabase db)
        {
            if ((entryUpdateInfo.Flags & SyncEntryChangedFlags.IsNew) != 0 ||
                (entryUpdateInfo.Flags & SyncEntryChangedFlags.Restored) != 0)
            {
                // TODO: Check for any other (invalid) flags that are set and throw exception
                // The item is new (on the source), so create the item on the destination. Metadata will be
                // set by this method as well.
                Logger.Info("Creating item using adapter");
                await adapter.CreateItemAsync(entryUpdateInfo.Entry).ConfigureAwait(false);

                // If the item is a file, copy the contents
                if (entryUpdateInfo.Entry.Type == SyncEntryType.File)
                {
                    Logger.Info("Copying file contents to new item");
                    await this.CopyFileAsync(
                            entryUpdateInfo.OriginatingAdapter,
                            adapter,
                            entryUpdateInfo,
                            throttlingManager)
                        .ConfigureAwait(false);
                }
            }
            else if ((entryUpdateInfo.Flags & SyncEntryChangedFlags.Deleted) != 0)
            {
                // TODO: Check for any other (invalid) flags that are set and throw exception
                // Delete the file on the adapter (the actual file). The entry will NOT be deleted from the
                // database as a part of this method.
                Logger.Info("Deleting item using adapter");
                adapter.DeleteItem(entryUpdateInfo.Entry);
            }
            else if ((entryUpdateInfo.Flags & SyncEntryChangedFlags.IsUpdated) != 0 ||
                     (entryUpdateInfo.Flags & SyncEntryChangedFlags.Renamed) != 0)
            {
                // If IsUpdated is true (which is possible along with a rename) and the item is a file, update
                // the file contents.
                if (entryUpdateInfo.Entry.Type == SyncEntryType.File)
                {
                    Logger.Info("Copying file contents to existing item");
                    await this.CopyFileAsync(
                            entryUpdateInfo.OriginatingAdapter,
                            adapter,
                            entryUpdateInfo,
                            throttlingManager)
                        .ConfigureAwait(false);
                }

                // The item was either renamed or the metadata was updated. Either way, this will be handled
                // by the UpdateItem call to the adapter.
                Logger.Info("Updating item using adapter");
                adapter.UpdateItem(entryUpdateInfo, entryUpdateInfo.Flags);

                if ((entryUpdateInfo.Flags & SyncEntryChangedFlags.CreatedTimestamp) != 0)
                {
                    //entryUpdateInfo.CreationDateTimeUtcNew =
                } 
            }
            else
            {
                throw new NotImplementedException("Invalid flags combination");
            }

            // The operation succeeded. Update state variables.
            entryUpdateInfo.State = EntryUpdateState.Succeeded;

            entryUpdateInfo.Entry.EntryLastUpdatedDateTimeUtc = DateTime.UtcNow;

            // We are about to add or update the entry, so clear the NotSynchronized bit from the status.
            entryUpdateInfo.Entry.State &= ~SyncEntryState.NotSynchronized;

            // If this is a new entry, we need to add it to the database. Otherwise, we need to update the existing entry 
            // in the database using the ID of the entry.
            if ((entryUpdateInfo.Flags & SyncEntryChangedFlags.IsNew) != 0)
            {
                // When adding an entry for the first time, there is a good chance that the parent was just added as well. In this 
                // case, the ParentId property on this entry will be 0 (since we didn't know it when this entry was created). We fix
                // this by overwriting the ParentId property is the ID from the parent entry (which was set when that entry was added
                // to the database.
                if (entryUpdateInfo.Entry.ParentId == 0)
                {
                    // If this assert fails, check why the parent entry is null. To add a new entry to the database, we need to have
                    // either the parent ID or the parent entry (where we can get the parent ID from).
                    Pre.Assert(entryUpdateInfo.Entry.ParentEntry != null, "entryUpdateInfo.Entry.ParentEntry != null");

                    // Update our own parent ID
                    entryUpdateInfo.Entry.ParentId = entryUpdateInfo.Entry.ParentEntry.Id;
                }

                // Double-check that we have a legit parent ID
                Pre.Assert(entryUpdateInfo.Entry.ParentId != null, "entryUpdateInfo.Entry.ParentId != null");

                db.Entries.Add(entryUpdateInfo.Entry);

                // Ensure that there are more than one adapter entry.
                Pre.Assert(entryUpdateInfo.Entry.AdapterEntries.Count > 1, "entryUpdateInfo.Entry.AdapterEntries.Count > 1");
                db.AdapterEntries.AddRange(entryUpdateInfo.Entry.AdapterEntries);
            }
            else
            {
                db.UpdateSyncEntry(entryUpdateInfo.Entry);
            }

            Logger.Info("Item processed successfully");

            return true;
        }

        private async Task CopyFileAsync(
            AdapterBase fromAdapter, 
            AdapterBase toAdapter, 
            EntryUpdateInfo updateInfo,
            ThrottlingManager throttlingManager)
        {
            Stream fromStream = null;
            Stream toStream = null;

            try
            {
                fromStream = fromAdapter.GetReadStreamForEntry(updateInfo.Entry);
                toStream = toAdapter.GetWriteStreamForEntry(updateInfo.Entry, updateInfo.Entry.Size);

                TransferResult result = await this.TransferDataWithHashAsync(
                        fromStream,
                        toStream,
                        updateInfo,
                        throttlingManager)
                    .ConfigureAwait(false);

                updateInfo.SizeNew = result.BytesTransferred;
                updateInfo.Sha1HashNew = result.Sha1Hash;
                updateInfo.Md5HashNew = result.Md5Hash;

                updateInfo.Entry.Sha1Hash = result.Sha1Hash;
                updateInfo.Entry.Md5Hash = result.Md5Hash;
            }
            finally
            {
                fromStream?.Close();
                toStream?.Close();
            }
        }

        private class TransferResult
        {
            public long BytesTransferred { get; set; }
            public byte[] Sha1Hash { get; set; }
            public byte[] Md5Hash { get; set; }
        }

        private async Task<TransferResult> TransferDataWithHashAsync(
            Stream sourceStream, 
            Stream destinationStream, 
            EntryUpdateInfo updateInfo,
            ThrottlingManager throttlingManager)
        {
            SHA1Cng sha1 = null;
            MD5Cng md5 = null;

            try
            {
                sha1 = new SHA1Cng();
                md5 = new MD5Cng();

                byte[] buffer = new byte[transferBufferSize];
                int read;
                long readTotal = 0;

                while (true)
                {
                    // If we are using a throttling manager, get the necessary number of tokens to
                    // transfer the data
                    if (throttlingManager != null)
                    {
                        int tokens = 0;
                        while (true)
                        {
                            tokens += throttlingManager.GetTokens(transferBufferSize - tokens);
                            if (tokens >= transferBufferSize)
                            {
                                break;
                            }

                            await Task.Delay(10, this.cancellationTokenSource.Token).ConfigureAwait(false);
                        }
                    }

                    // Read data from the source adapter
                    if ((read = sourceStream.Read(buffer, 0, buffer.Length)) <= 0)
                    {
                        // Read the end of the stream
                        break;
                    }

                    // Increment the total number of bytes read from the source adapter
                    readTotal += read;

                    // Pass the data through the required hashing algorithms
                    sha1.TransformBlock(buffer, 0, read, null, 0);
                    md5.TransformBlock(buffer, 0, read, null, 0);

                    // Write the data to the destination adapter
                    destinationStream.Write(buffer, 0, read);

                    // Increment the total number of bytes written to the desination adapter
                    this.bytesCompleted += read;

                    if (this.syncProgressUpdateStopwatch.ElapsedMilliseconds > 100)
                    {
                        this.RaiseSyncProgressChanged(updateInfo);
                        this.syncProgressUpdateStopwatch.Restart();
                    }
                }

                sha1.TransformFinalBlock(buffer, 0, read);
                md5.TransformFinalBlock(buffer, 0, read);

                return new TransferResult
                {
                    BytesTransferred = readTotal,
                    Sha1Hash = sha1.Hash,
                    Md5Hash = md5.Hash
                };
            }
            finally
            {
                sha1?.Dispose();
                md5?.Dispose();
            }
        }

        public static SyncRun FromHistoryEntry(SyncRelationship relationship, SyncHistoryData history)
        {
            SyncRun run = new SyncRun(relationship)
            {
                Id = history.Id,
                StartTime = history.Start,
                EndTime = history.End,
                FilesTotal = history.TotalFiles,
                BytesTotal = history.TotalBytes,
                SyncResult = history.Result
            };

            return run;
        }
    }
}