namespace SyncPro.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Data.Entity;
    using System.Diagnostics;
    using System.Linq;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;

    using SyncPro.Adapters;
    using SyncPro.Data;
    using SyncPro.Tracing;
    using SyncPro.Utility;

    /// <summary>
    /// Contains the core logic for synchronizing (copying) files between two adapters.
    /// </summary>
    public class SyncJob : JobBase
    {
        private readonly SyncRelationship relationship;

        private long filesCompleted;

        private int? syncHistoryId;

        private long bytesCompleted;

        private Stopwatch syncProgressUpdateStopwatch;

        private X509Certificate2 encryptionCertificate;

        /// <summary>
        /// The unique Id of this sync job (unique within the relationship)
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The analysis result indicating the items that are to be synchronized. 
        /// </summary>
        public AnalyzeRelationshipResult AnalyzeResult { get; }

        public event EventHandler<SyncJobProgressInfo> ProgressChanged;

        private readonly Queue<Tuple<DateTime, long>> throughputCalculationCache = new Queue<Tuple<DateTime, long>>();

        private readonly object progressLock = new object();

        private void RaiseSyncProgressChanged(EntryUpdateInfo updateInfo, long bytesCopied)
        {
            lock (this.progressLock)
            {
                this.bytesCompleted += bytesCopied;

                this.throughputCalculationCache.Enqueue(
                    new Tuple<DateTime, long>(
                        DateTime.Now,
                        this.bytesCompleted));

                int bytesPerSecond = 0;
                if (this.throughputCalculationCache.Count() > 10)
                {
                    Tuple<DateTime, long> oldest;

                    if (this.throughputCalculationCache.Count() > 100)
                    {
                        oldest = this.throughputCalculationCache.Dequeue();
                    }
                    else
                    {
                        oldest = this.throughputCalculationCache.Peek();
                    }

                    TimeSpan delay = DateTime.Now - oldest.Item1;
                    long bytes = this.bytesCompleted - oldest.Item2;

                    bytesPerSecond = Convert.ToInt32(Math.Floor(bytes / delay.TotalSeconds));
                }

                this.ProgressChanged?.Invoke(
                    this,
                    new SyncJobProgressInfo(
                        updateInfo,
                        this.FilesTotal,
                        Convert.ToInt32(Interlocked.Read(ref this.filesCompleted)),
                        this.BytesTotal,
                        this.bytesCompleted,
                        bytesPerSecond));
            }
        }

        /// <summary>
        /// Create a new <see cref="SyncJob"/>
        /// </summary>
        /// <param name="relationship">The relationship to be synchonized</param>
        public SyncJob(SyncRelationship relationship)
            : this(relationship, null)
        {
        }

        private SyncJob(SyncRelationship relationship, DateTime startTime, DateTime? endTime)
            : base(relationship, startTime, endTime)
        {
        }

        /// <summary>
        /// Create a new <see cref="SyncJob"/>
        /// </summary>
        /// <param name="relationship">The relationship to be synchonized</param>
        /// <param name="result">The analyze result containing the file to synchronize.</param>
        public SyncJob(SyncRelationship relationship, AnalyzeRelationshipResult result)
            : base(relationship)
        {
            this.relationship = relationship;
            this.AnalyzeResult = result;
        }

        protected override async Task ExecuteTask()
        {
            if (this.AnalyzeResult == null)
            {
                throw new InvalidOperationException("No analyze result to synchronize");
            }

            if (this.TriggerType == SyncTriggerType.Undefined)
            {
                throw new InvalidOperationException("TriggerType cannot be Undefined");
            }

            if (this.AnalyzeResult.SyncJobResult == JobResult.Success)
            {
                return;
            }

            // Create a new sync history entry (except for analyze-only runs)
            this.CreateNewSyncJobHistory();

            try
            {
                if (this.AnalyzeResult.IsUpToDate)
                {
                    this.JobResult = JobResult.NotRun;
                }
                else
                {
                    // Run actual synchronization of entries
                    await this.SyncInternalAsync().ConfigureAwait(false);
                }

                this.AnalyzeResult.SyncJobResult = this.JobResult;

                // If sync was run successfully commit the tracked changes for each adapter
                if (this.JobResult == JobResult.Success)
                {
                    await this.AnalyzeResult.CommitTrackedChangesAsync(this.relationship).ConfigureAwait(false);
                }

                // If a sync job was requested but not needed because all of the files are already up to date, commit change
                // if needed (because the delta token was refreshed).
                if (this.AnalyzeResult.IsUpToDate)
                {
                    await this.AnalyzeResult.CommitTrackedChangesAsync(this.relationship).ConfigureAwait(false);
                }
            }
            finally
            {
                this.EndTime = DateTime.Now;

                this.SaveSyncJobHistory();

                try
                {
                    await this.SendReport();
                }
                catch (Exception e)
                {
                    Logger.LogException(e, "Failed to send report email.");
                }
            }
        }

        private async Task SendReport()
        {
            if (this.Relationship.TriggerType == SyncTriggerType.Continuous)
            {
                // Delay sending a report until a later time
                // TODO
                return;
            }

            if (!this.Relationship.SendSyncJobReports)
            {
                Logger.Info("Skipping report send. Reports are not enabled for this relationship.");
                return;
            }

            if (Global.AppConfig.EmailReporting == null)
            {
                Logger.Info("Skipping report send. No global configuration found.");
                return;
            }

            SyncReport report = SyncReport.Create(this);
            await report.SendAsync();
        }

        private void SaveSyncJobHistory()
        {
            using (var db = this.relationship.GetDatabase())
            {
                SyncHistoryData historyData = db.History.First(h => h.Id == this.syncHistoryId);

                historyData.End = this.EndTime;
                historyData.TotalFiles = this.FilesTotal;
                historyData.TotalBytes = this.BytesTotal;
                historyData.Result = this.JobResult;

                db.SaveChanges();
            }
        }

        private void CreateNewSyncJobHistory()
        {
            // Create a history entry for this job in the database
            using (var db = this.relationship.GetDatabase())
            {
                SyncHistoryData syncHistory = new SyncHistoryData()
                {
                    Start = this.StartTime,
                    End = this.EndTime,
                    Result = this.JobResult,
                    TriggeredBy = this.TriggerType,
                };

                db.History.Add(syncHistory);
                db.SaveChanges();
                this.syncHistoryId = syncHistory.Id;
            }
        }

        public SyncTriggerType TriggerType { get; set; }

        /// <summary>
        /// Synchronzied changes previously determined by the SyncAnalyzer.
        /// </summary>
        /// <returns>Nothing (async task)</returns>
        private async Task SyncInternalAsync()
        {
            Logger.SynchronizeChangesStart(
                new Dictionary<string, object>
                {
                    { "AnalyzeResultId", this.AnalyzeResult.Id },
                });

            if (this.relationship.EncryptionMode != Configuration.EncryptionMode.None)
            {
                X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadOnly);

                var cert = store.Certificates.Find(
                    X509FindType.FindByThumbprint,
                    this.relationship.EncryptionCertificateThumbprint,
                    false);

                if (cert.Count == 0)
                {
                    throw new Exception(
                        string.Format(
                            "Failed to locate a certificate in the CurrentUser/My store with thumbprint {0}",
                            this.relationship.EncryptionCertificateThumbprint));
                }

                this.encryptionCertificate = cert[0];
            }

            // Get the list of EntryUpdateInfo object for each change that needs to be synchronzied. Be sure that we are
            // gathering the results from each adapter, which is needed in bidirectional sync.
            List<EntryUpdateInfo> updateList = this.AnalyzeResult.AdapterResults.SelectMany(r => r.Value.EntryResults).ToList();

            // Sort the updates according to the specific logic in the EntryProcessingSorter
            updateList.Sort(new EntryProcessingSorter());

            this.FilesTotal = updateList.Count;
            this.filesCompleted = 0;

            this.BytesTotal = updateList
                .Where(item => item.HasSyncEntryFlag(SyncEntryChangedFlags.IsNewOrUpdated))
                .Aggregate((long)0, (current, info) => current + info.Entry.GetSize(this.relationship, SyncEntryPropertyLocation.Source));
            this.bytesCompleted = 0;

            this.syncProgressUpdateStopwatch = new Stopwatch();
            this.syncProgressUpdateStopwatch.Start();

            ThrottlingManager throttlingManager = null;
            if (this.relationship.IsThrottlingEnabled)
            {
                Logger.Debug(
                    "SyncJob will use throttling manager with rate={0} B/sec",
                    this.relationship.IsThrottlingEnabled);

                int bytesPerSecond = this.relationship.ThrottlingValue * this.relationship.ThrottlingScaleFactor;
                throttlingManager = new ThrottlingManager(bytesPerSecond, bytesPerSecond * 3);
            }

            Stopwatch syncTimeStopwatch = new Stopwatch();

            using (throttlingManager)
            using (var db = this.relationship.GetDatabase())
            {
                syncTimeStopwatch.Start();

#if SYNC_THREAD_POOLS
                using (SemaphoreSlim semaphore = new SemaphoreSlim(8, 8))
                {
                    await this.SyncInteralWithPoolingAsync(
                        throttlingManager,
                        db,
                        updateList,
                        semaphore);
                }
#else
                await this.SyncInteralWithoutPoolingAsync(
                    throttlingManager,
                    db,
                    updateList);
#endif
                syncTimeStopwatch.Stop();
            }

            Logger.Info("Total sync time: " + syncTimeStopwatch.Elapsed);

            // Invoke the ProgressChanged event one final time with a null EntryUpdateInfo object to flush
            // out the final values for files and bytes.
            this.RaiseSyncProgressChanged(null, 0);

            if (this.CancellationToken.IsCancellationRequested)
            {
                this.JobResult = JobResult.Cancelled;
            }
            else if (updateList.Any(e => e.State == EntryUpdateState.Failed))
            {
                this.JobResult = JobResult.Error;
            }
            else
            {
                this.JobResult = JobResult.Success;
            }

            using (var db = this.relationship.GetDatabase())
            {
                var historyData = db.History.First(h => h.Id == this.syncHistoryId);

                historyData.End = this.EndTime;
                historyData.TotalFiles = Convert.ToInt32(this.filesCompleted);
                historyData.TotalBytes = this.bytesCompleted;
                historyData.Result = this.JobResult;

                db.SaveChanges();
            }
        }

#if !SYNC_THREAD_POOLS
        private async Task<bool> SyncInteralWithoutPoolingAsync(
            ThrottlingManager throttlingManager,
            SyncDatabase db,
            List<EntryUpdateInfo> updateList)
        {
            foreach (EntryUpdateInfo entryUpdateInfo in updateList)
            {
                if (this.CancellationToken.IsCancellationRequested)
                {
                    return true;
                }

                if (entryUpdateInfo.State == EntryUpdateState.Succeeded)
                {
                    Logger.Debug(
                        "Skipping synchronization for already-synchronized entry {0} ({1})",
                        entryUpdateInfo.Entry.Id,
                        entryUpdateInfo.Entry.Name);

                    continue;
                }

                Logger.Debug(
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

                string message = string.Empty;

                try
                {
                    fileProcessed = await this.ProcessEntryAsync(
                            entryUpdateInfo,
                            adapter,
                            throttlingManager,
                            db)
                        .ConfigureAwait(false);

                    message = "The change was successfully synchronized";
                }
                catch (TaskCanceledException)
                {
                    Logger.Warning("Processing was cancelled");

                    entryUpdateInfo.ErrorMessage = "Processing was cancelled";
                    entryUpdateInfo.State = EntryUpdateState.Failed;

                    message = "The change was cancelled during processing";
                }
                catch (Exception exception)
                {
                    Logger.Warning(
                        "Processing failed with {0}: {1}",
                        exception.GetType().FullName,
                        exception.Message);

                    entryUpdateInfo.ErrorMessage = exception.Message;
                    entryUpdateInfo.State = EntryUpdateState.Failed;

                    message = "An error occurred while synchronzing the changed.";
                }
                finally
                {
                    Logger.ChangeSynchronzied(
                        Logger.BuildEventMessageWithProperties(
                            message,
                            new Dictionary<string, object>()
                            {
                                { "AnalyzeResultId", this.AnalyzeResult.Id },
                            }));
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

            return false;
        }
#endif

#if SYNC_THREAD_POOLS
        private async Task<bool> SyncInteralWithPoolingAsync(
            ThrottlingManager throttlingManager,
            SyncDatabase db,
            List<EntryUpdateInfo> updateList,
            SemaphoreSlim semaphore)
        {
            List<Task> activeTasks = new List<Task>();

            int addedTasks = 0;
            int removedTasks = 0;

            foreach (EntryUpdateInfo entryUpdateInfo in updateList)
            {
                if (this.CancellationToken.IsCancellationRequested)
                {
                    break;
                }

                if (entryUpdateInfo.State == EntryUpdateState.Succeeded)
                {
                    Logger.Debug(
                        "Skipping synchronization for already-synchronized entry {0} ({1})",
                        entryUpdateInfo.Entry.Id,
                        entryUpdateInfo.Entry.Name);

                    continue;
                }

                await semaphore.WaitAsync().ConfigureAwait(false);

                Logger.Debug(
                    "Processing update for entry {0} ({1}) with flags {2}",
                    entryUpdateInfo.Entry.Id,
                    entryUpdateInfo.Entry.Name,
                    string.Join(",", StringExtensions.GetSetFlagNames<SyncEntryChangedFlags>(entryUpdateInfo.Flags)));

                Pre.Assert(entryUpdateInfo.Flags != SyncEntryChangedFlags.None, "entryUpdateInfo.Flags != None");

                // Find the adapter that this change should be synchronized to. This will be the 
                // adapter that is not the adapter originates from.
                // Dev Note: This is currently designed to only handle two adapters (one source and
                // one destination). When support is added to support more than 2 adapters in a 
                // relationship, the below code will need to be changed to call ProcessEntryAsync
                // for each adapter that the change needs to be synchronized to.
                AdapterBase adapter = this.relationship.Adapters.FirstOrDefault(
                    a => a.Configuration.Id != entryUpdateInfo.OriginatingAdapter.Configuration.Id);
                Pre.Assert(adapter != null, "adapter != null");

                EntryProcessingContext context = new EntryProcessingContext(
                    entryUpdateInfo,
                    semaphore);

                // New directories and deletes are processed synchronously. They are already pre-ordered
                // so that parent directories will be created before children and deletes of children 
                // will occur before parents.
                if (entryUpdateInfo.HasSyncEntryFlag(SyncEntryChangedFlags.NewDirectory) ||
                    entryUpdateInfo.HasSyncEntryFlag(SyncEntryChangedFlags.Deleted))
                {
                    await this.ProcessEntryAsync(
                            entryUpdateInfo,
                            adapter,
                            throttlingManager,
                            db)
                        .ContinueWith(this.ProcessEntryCompleteAsync, context)
                        .ConfigureAwait(false);
                }
                else
                {
                    removedTasks += activeTasks.RemoveAll(t => t.IsCompleted);

                    Task processingTask = Task.Run(
                        async () =>
                        {
                            await this.ProcessEntryAsync(
                                    entryUpdateInfo,
                                    adapter,
                                    throttlingManager,
                                    db)
                                .ContinueWith(this.ProcessEntryCompleteAsync, context)
                                .ConfigureAwait(false);
                        });

                    activeTasks.Add(processingTask);
                    addedTasks++;
                }
            }

            await Task.WhenAll(activeTasks).ConfigureAwait(false);
            removedTasks += activeTasks.Count;

            Logger.Debug(
                "SyncInteralWithPoolingAsync completed with {0} added, {1} removed",
                addedTasks,
                removedTasks);

            return false;
        }

        private volatile object dbLock = new object();

        private class EntryProcessingContext
        {
            public EntryUpdateInfo EntryUpdateInfo { get; }

            public SemaphoreSlim Semaphore { get; }

            public EntryProcessingContext(
                EntryUpdateInfo entryUpdateInfo,
                SemaphoreSlim semaphore)
            {
                this.EntryUpdateInfo = entryUpdateInfo;
                this.Semaphore = semaphore;
            }
        }

        private void ProcessEntryCompleteAsync(Task<bool> task, object context)
        {
            EntryProcessingContext ctx = (EntryProcessingContext)context;

            try
            {
                string message;
                if (task.IsCanceled)
                {
                    Logger.Warning("Processing was cancelled");

                    ctx.EntryUpdateInfo.ErrorMessage = "Processing was cancelled";
                    ctx.EntryUpdateInfo.State = EntryUpdateState.Failed;

                    message = "The change was cancelled during processing";
                }
                else if (task.Exception != null)
                {
                    Exception ex = task.Exception;
                    if (task.Exception.InnerExceptions.Count == 1 && 
                        task.Exception.InnerException != null)
                    {
                        ex = task.Exception.InnerException;
                    }

                    Logger.Warning(
                        "Processing failed with {0}: {1}",
                        ex.GetType().FullName,
                        ex.Message);

                    ctx.EntryUpdateInfo.ErrorMessage = ex.Message;
                    ctx.EntryUpdateInfo.State = EntryUpdateState.Failed;

                    message = "An error occurred while synchronzing the changed.";
                }
                else
                {
                    message = "The change was successfully synchronized";

                    // While accessing task.Result will throw an exception if an exception
                    // was thrown in the task, that case should have already been handled
                    // above, so this should be a safe call to make (ie will not except).
                    if (task.Result)
                    {
                        Interlocked.Increment(ref this.filesCompleted);

                        SyncHistoryEntryData historyEntry =
                            ctx.EntryUpdateInfo.CreateSyncHistoryEntryData();

                        Pre.Assert(this.syncHistoryId != null, "this.syncHistoryId != null");

                        historyEntry.SyncHistoryId = this.syncHistoryId.Value;
                        if (ctx.EntryUpdateInfo.Entry.Id != 0)
                        {
                            historyEntry.SyncEntryId = ctx.EntryUpdateInfo.Entry.Id;
                        }
                        else
                        {
                            historyEntry.SyncEntry = ctx.EntryUpdateInfo.Entry;
                        }

                        lock (this.dbLock)
                        {
                            using (var db = this.Relationship.GetDatabase())
                            {
                                db.HistoryEntries.Add(historyEntry);
                                db.SaveChanges();
                            }
                        }
                    }
                }

                Logger.Debug(
                    "Finished processing update for entry {0} ({1})",
                    ctx.EntryUpdateInfo.Entry.Id,
                    ctx.EntryUpdateInfo.Entry.Name);

                Logger.ChangeSynchronzied(
                    Logger.BuildEventMessageWithProperties(
                        message,
                        new Dictionary<string, object>()
                        {
                            { "AnalyzeResultId", this.AnalyzeResult.Id },
                        }));
            }
            catch (Exception e)
            {
                Logger.Info(
                    "Caught an exception while completing entry processing. " + e);
            }
            finally
            {
                ctx.Semaphore.Release();
            }
        }
#endif

        private void CopyProgressChanged(CopyProgressInfo obj)
        {
            this.RaiseSyncProgressChanged(obj.EntryUpdateInfo, obj.BytesCopied);
        }

        private async Task<bool> ProcessEntryAsync(
            EntryUpdateInfo entryUpdateInfo,
            AdapterBase destinationAdapter,
            ThrottlingManager throttlingManager,
            SyncDatabase db)
        {
            if (entryUpdateInfo.HasSyncEntryFlag(SyncEntryChangedFlags.IsNew) ||
                entryUpdateInfo.HasSyncEntryFlag(SyncEntryChangedFlags.Restored))
            {
                // TODO: Check for any other (invalid) flags that are set and throw exception
                // If the item being created is a directory, use the CreateItemAsync method to create the 
                // item, since it does not have any content. Otherwise, use the CopyFileAsync method, which
                // will create the item when setting the content.
                if (entryUpdateInfo.HasSyncEntryFlag(SyncEntryChangedFlags.NewDirectory))
                {
                    if (entryUpdateInfo.HasSyncEntryFlag(SyncEntryChangedFlags.DestinationExists))
                    {
                        Logger.Debug("Directory already exists at destination.");
                        destinationAdapter.AddAdapterEntry(entryUpdateInfo);

                        if (entryUpdateInfo.HasSyncEntryFlag(SyncEntryChangedFlags.IsUpdated))
                        {
                            // The metadata for the item was updated, so update the item
                            Logger.Debug("Updating existing item using adapter");
                            destinationAdapter.UpdateItem(entryUpdateInfo, entryUpdateInfo.Flags);
                        }
                    }
                    else
                    {
                        Logger.Debug("Creating new directory");
                        await destinationAdapter.CreateItemAsync(entryUpdateInfo.Entry).ConfigureAwait(false);
                    }
                }
                else
                {
                    if (entryUpdateInfo.HasSyncEntryFlag(SyncEntryChangedFlags.DestinationExists))
                    {
                        Logger.Debug("File already exists at destination.");
                        destinationAdapter.AddAdapterEntry(entryUpdateInfo);

                        if (entryUpdateInfo.HasSyncEntryFlag(SyncEntryChangedFlags.IsUpdated))
                        {
                            // The metadata for the item was updated, so update the item
                            Logger.Debug("Updating existing item using adapter");
                            destinationAdapter.UpdateItem(entryUpdateInfo, entryUpdateInfo.Flags);
                        }
                    }
                    else
                    {
                        FileCopyHelper fileCopyHelper = new FileCopyHelper(
                            this.Relationship,
                            entryUpdateInfo.OriginatingAdapter,
                            destinationAdapter,
                            entryUpdateInfo,
                            throttlingManager,
                            this.encryptionCertificate,
                            this.CancellationToken,
                            this.CopyProgressChanged);

                        Logger.Debug("Creating item with content");
                        await fileCopyHelper.CopyFileAsync().ConfigureAwait(false);
                    }
                }
            }
            else if (entryUpdateInfo.HasSyncEntryFlag(SyncEntryChangedFlags.Deleted))
            {
                // TODO: Check for any other (invalid) flags that are set and throw exception
                // Delete the file on the adapter (the actual file). The entry will NOT be deleted from the
                // database as a part of this method.
                Logger.Debug("Deleting item using adapter");
                destinationAdapter.DeleteItem(entryUpdateInfo.Entry);

                // Persist the deleted state of the entry. This will be saved in the db.
                entryUpdateInfo.Entry.State |= SyncEntryState.IsDeleted;
            }
            else if (entryUpdateInfo.HasSyncEntryFlag(SyncEntryChangedFlags.IsUpdated) ||
                     entryUpdateInfo.HasSyncEntryFlag(SyncEntryChangedFlags.Renamed) ||
                     entryUpdateInfo.HasSyncEntryFlag(SyncEntryChangedFlags.Moved))
            {
                bool contentChanged =
                    entryUpdateInfo.HasSyncEntryFlag(SyncEntryChangedFlags.CreatedTimestamp) ||
                    entryUpdateInfo.HasSyncEntryFlag(SyncEntryChangedFlags.ModifiedTimestamp) ||
                    entryUpdateInfo.HasSyncEntryFlag(SyncEntryChangedFlags.FileSize) ||
                    entryUpdateInfo.HasSyncEntryFlag(SyncEntryChangedFlags.Sha1Hash);

                // If IsUpdated is true (which is possible along with a rename) and the item is a file, update
                // the file contents.
                if (contentChanged && entryUpdateInfo.Entry.Type == SyncEntryType.File)
                {
                    FileCopyHelper fileCopyHelper = new FileCopyHelper(
                        this.Relationship,
                        entryUpdateInfo.OriginatingAdapter,
                        destinationAdapter,
                        entryUpdateInfo,
                        throttlingManager,
                        this.encryptionCertificate,
                        this.CancellationToken,
                        this.CopyProgressChanged);

                    Logger.Debug("Copying file contents to existing item");
                    await fileCopyHelper.CopyFileAsync().ConfigureAwait(false);
                }

                // The item was either renamed or the metadata was updated. Either way, this will be handled
                // by the UpdateItem call to the adapter.
                Logger.Debug("Updating item using adapter");
                destinationAdapter.UpdateItem(entryUpdateInfo, entryUpdateInfo.Flags);
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

                // Ensure that there are more than one adapter entry.
                Pre.Assert(entryUpdateInfo.Entry.AdapterEntries.Count > 1, "entryUpdateInfo.Entry.AdapterEntries.Count > 1");

                lock (this.dbLock)
                {
                    db.Entries.Add(entryUpdateInfo.Entry);
                    db.AdapterEntries.AddRange(entryUpdateInfo.Entry.AdapterEntries);
                    db.SaveChanges();
                }
            }
            else
            {
                lock (this.dbLock)
                {
                    db.Entry(entryUpdateInfo.Entry).State = EntityState.Modified;
                    db.SaveChanges();
                }
            }

            Logger.Debug("Item processed successfully");

            return true;
        }

        public static SyncJob FromHistoryEntry(SyncRelationship relationship, SyncHistoryData history)
        {
            SyncJob job = new SyncJob(relationship, history.Start, history.End)
            {
                Id = history.Id,
                FilesTotal = history.TotalFiles,
                BytesTotal = history.TotalBytes,
                JobResult = history.Result
            };

            return job;
        }
    }
}