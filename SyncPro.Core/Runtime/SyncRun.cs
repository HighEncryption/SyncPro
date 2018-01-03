namespace SyncPro.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;

    using SyncPro.Adapters;
    using SyncPro.Configuration;
    using SyncPro.Data;
    using SyncPro.Tracing;

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

    /// <summary>
    /// Contains the core logic for synchronizing (copying) files between two adapters.
    /// </summary>
    public class SyncRun
    {
        /// <summary>
        /// The buffer size used for copying data between adapters (currently 64k).
        /// </summary>
        private const int transferBufferSize = 0x10000;

        private readonly SyncRelationship relationship;

        private long filesCompleted;

        private int? syncHistoryId;

        private long bytesCompleted;

        private Task syncTask;

        private CancellationTokenSource cancellationTokenSource;

        private Stopwatch syncProgressUpdateStopwatch;

        private X509Certificate2 encryptionCertificate;

        /// <summary>
        /// The unique Id of this sync run (unique within the relationship)
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The datetime when the synchronization process was started
        /// </summary>
        public DateTime StartTime { get; private set; }

        /// <summary>
        /// The datetime when the synchronization process finished
        /// </summary>
        public DateTime? EndTime { get; private set; }

        /// <summary>
        /// The analysis result indicating the items that are to be synchronized. If this property is set
        /// prior to starting the sync run, the items specified in the analyze result will be used to 
        /// determine what is synchronized. If this property is not set, the analysis will be done as
        /// a part of the sync run, and those items will be synchronized.
        /// </summary>
        public AnalyzeRelationshipResult AnalyzeResult { get; set; }

        /// <summary>
        /// Indicates whether the sync run has started
        /// </summary>
        public bool HasStarted => this.StartTime != DateTime.MinValue;

        /// <summary>
        /// Indicates whether the sync run has finished
        /// </summary>
        public bool HasFinished => this.StartTime != DateTime.MinValue;

        /// <summary>
        /// The total number of files synchronized
        /// </summary>
        public int FilesTotal { get; private set; }

        /// <summary>
        /// The total number of bytes synchronized
        /// </summary>
        public long BytesTotal { get; private set; }

        /// <summary>
        /// The result of the sync run
        /// </summary>
        public SyncRunResult SyncResult { get; private set; }

        /// <summary>
        /// Indicates whether this sync run is for analysis only (meaning that no items will 
        /// be synchronized).
        /// </summary>
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
                    Convert.ToInt32(Interlocked.Read(ref this.filesCompleted)), 
                    this.BytesTotal,
                    this.bytesCompleted,
                    bytesPerSecond));
        }

        /// <summary>
        /// Create a new <see cref="SyncRun"/>
        /// </summary>
        /// <param name="relationship">The relationship to be synchonized</param>
        public SyncRun(SyncRelationship relationship)
        {
            this.relationship = relationship;
        } // 420fe8033179cfb0ef21862d24bf6a1ec7df6c6d

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

        /// <summary>
        /// The main processing method for the sync run.
        /// </summary>
        /// <returns>Async task</returns>
        private async Task RunMainThread()
        {
            this.StartTime = DateTime.Now;
            this.relationship.State = SyncRelationshipState.Running;

            // Create a new sync history entry (except for analyze-only runs)
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
                .Aggregate((long)0, (current, info) => current + info.Entry.SourceSize);
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

            Stopwatch syncTimeStopwatch = new Stopwatch();

            using (throttlingManager)
            using (var db = this.relationship.GetDatabase())
            {
                syncTimeStopwatch.Start();

#if SYNC_THREAD_POOLS
                using (SemaphoreSlim semaphore = new SemaphoreSlim(5, 5))
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

            Logger.Verbose("Total sync time: " + syncTimeStopwatch.Elapsed);

            // Invoke the ProgressChanged event one final time with a null EntryUpdateInfo object to flush
            // out the final values for files and bytes.
            this.RaiseSyncProgressChanged(null);

            this.EndTime = DateTime.UtcNow;

            if (this.cancellationTokenSource.Token.IsCancellationRequested)
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
                historyData.TotalFiles = Convert.ToInt32(this.filesCompleted);
                historyData.TotalBytes = this.bytesCompleted;
                historyData.Result = this.SyncResult;

                db.SaveChanges();
            }
        }

        private async Task<bool> SyncInteralWithoutPoolingAsync(
            ThrottlingManager throttlingManager,
            SyncDatabase db,
            List<EntryUpdateInfo> updateList)
        {
            foreach (EntryUpdateInfo entryUpdateInfo in updateList)
            {
                if (this.cancellationTokenSource.Token.IsCancellationRequested)
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
                if (this.cancellationTokenSource.Token.IsCancellationRequested)
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
                    semaphore,
                    db);

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

                    var entryTask = this.ProcessEntryAsync(
                        entryUpdateInfo,
                        adapter,
                        throttlingManager,
                        db);

#pragma warning disable CS4014

                    // We want fire-and-forget behavior for this
                    var processingCompleteTask = entryTask
                        .ContinueWith(this.ProcessEntryCompleteAsync, context);

                    processingCompleteTask.ConfigureAwait(false);

#pragma warning restore CS4014

                    activeTasks.Add(processingCompleteTask);
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

            public SyncDatabase Db { get; }

            public EntryProcessingContext(
                EntryUpdateInfo entryUpdateInfo,
                SemaphoreSlim semaphore,
                SyncDatabase db)
            {
                this.EntryUpdateInfo = entryUpdateInfo;
                this.Semaphore = semaphore;
                this.Db = db;
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
                    Logger.Warning(
                        "Processing failed with {0}: {1}",
                        task.Exception.GetType().FullName,
                        task.Exception.Message);

                    ctx.EntryUpdateInfo.ErrorMessage = task.Exception.Message;
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
                        //historyEntry.SyncEntryId = ctx.EntryUpdateInfo.Entry.Id;
                        historyEntry.SyncEntry = ctx.EntryUpdateInfo.Entry;

                        lock (this.dbLock)
                        {
                            ctx.Db.HistoryEntries.Add(historyEntry);
                            ctx.Db.SaveChanges();
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
                Logger.Critical(
                    "Caught an exception while completing entry processing. " + e);
            }
            finally
            {
                ctx.Semaphore.Release();
            }
        }

        private async Task<bool> ProcessEntryAsync(
            EntryUpdateInfo entryUpdateInfo,
            AdapterBase adapter, // TODO: Rename to destinationAdapter
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
                    Logger.Debug("Creating item without content");
                    await adapter.CreateItemAsync(entryUpdateInfo.Entry).ConfigureAwait(false);
                }
                else
                {
                    Logger.Debug("Creating item with content");
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
                Logger.Debug("Deleting item using adapter");
                adapter.DeleteItem(entryUpdateInfo.Entry);
            }
            else if ((entryUpdateInfo.Flags & SyncEntryChangedFlags.IsUpdated) != 0 ||
                     (entryUpdateInfo.Flags & SyncEntryChangedFlags.Renamed) != 0)
            {
                // If IsUpdated is true (which is possible along with a rename) and the item is a file, update
                // the file contents.
                if (entryUpdateInfo.Entry.Type == SyncEntryType.File)
                {
                    Logger.Debug("Copying file contents to existing item");
                    await this.CopyFileAsync(
                            entryUpdateInfo.OriginatingAdapter,
                            adapter,
                            entryUpdateInfo,
                            throttlingManager)
                        .ConfigureAwait(false);
                }

                // The item was either renamed or the metadata was updated. Either way, this will be handled
                // by the UpdateItem call to the adapter.
                Logger.Debug("Updating item using adapter");
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

                // Ensure that there are more than one adapter entry.
                Pre.Assert(entryUpdateInfo.Entry.AdapterEntries.Count > 1, "entryUpdateInfo.Entry.AdapterEntries.Count > 1");

                lock (this.dbLock)
                {
                    db.Entries.Add(entryUpdateInfo.Entry);
                    db.AdapterEntries.AddRange(entryUpdateInfo.Entry.AdapterEntries);
                }
            }
            else
            {
                lock (this.dbLock)
                {
                    db.UpdateSyncEntry(entryUpdateInfo.Entry);
                }
            }

            Logger.Debug("Item processed successfully");

            return true;
        }

        /// <summary>
        /// Copy a file from the source adapter to the destination adapter
        /// </summary>
        /// <param name="fromAdapter">The source adapter</param>
        /// <param name="toAdapter">The destination adapter</param>
        /// <param name="updateInfo">The update information about the file being copied</param>
        /// <param name="throttlingManager">The throtting manager</param>
        /// <returns>The async task</returns>
        private async Task CopyFileAsync(
            AdapterBase fromAdapter, 
            AdapterBase toAdapter, 
            EntryUpdateInfo updateInfo,
            ThrottlingManager throttlingManager)
        {
            Stream fromStream = null;
            Stream toStream = null;

            EncryptionManager encryptionManager = null;

            try
            {
                long writeStreamLength = updateInfo.Entry.SourceSize;

                if (this.relationship.EncryptionMode == EncryptionMode.Encrypt)
                {
                    short padding;
                    writeStreamLength = EncryptionManager.CalculateEncryptedFileSize(
                        updateInfo.Entry.SourceSize,
                        out padding);
                }
                else if (this.relationship.EncryptionMode == EncryptionMode.Decrypt)
                {
                    short padding;
                    writeStreamLength = EncryptionManager.CalculateDecryptedFileSize(
                        updateInfo.Entry.SourceSize,
                        out padding);
                }

                fromStream = fromAdapter.GetReadStreamForEntry(updateInfo.Entry);
                toStream = toAdapter.GetWriteStreamForEntry(updateInfo.Entry, writeStreamLength);

                if (this.relationship.EncryptionMode != EncryptionMode.None)
                {
                    // Create a copy of the certificate from the original cert's handle. A unique copy is required
                    // because the encryption manager will dispose of the RSA CSP derived from the cert, and will 
                    // cause an ObjectDisposedException on the next file encryption
                    X509Certificate2 certificate = new X509Certificate2(this.encryptionCertificate.Handle);

                    encryptionManager = new EncryptionManager(
                        certificate,
                        EncryptionMode.Encrypt,
                        toStream,
                        updateInfo.Entry.SourceSize);
                }

                TransferResult result = await this.TransferDataWithHashAsync(
                        fromStream,
                        toStream,
                        updateInfo,
                        throttlingManager,
                        encryptionManager)
                    .ConfigureAwait(false);

                // Add the transfer information back to the update info (so that it can be persisted later)
                updateInfo.SourceSizeNew = result.BytesRead;
                updateInfo.DestinationSizeNew = result.BytesWritten;

                updateInfo.SourceSha1HashNew = result.Sha1Hash;
                updateInfo.SourceMd5HashNew = result.Md5Hash;

                if (encryptionManager != null)
                {
                    updateInfo.DestinationSha1HashNew = result.TransformedSha1Hash;
                    updateInfo.DestinationMd5HashNew = result.TransformedMd5Hash;
                }

                // Add the hash information to the entry that was copied
                updateInfo.Entry.SourceSha1Hash = result.Sha1Hash;
                updateInfo.Entry.DestinationSha1Hash = result.TransformedSha1Hash ?? result.Sha1Hash;
                updateInfo.Entry.SourceMd5Hash = result.Md5Hash;
                updateInfo.Entry.DestinationMd5Hash = result.TransformedMd5Hash ?? result.Md5Hash;
            }
            finally
            {
                // Finalize the file transfer at the adapter-level. This method allows the adapter to call
                // any necessary method to complete the transfer. Note that this method MUST be called in the 
                // finally block (so that it is called even when the transfer throws an exception) and before 
                // disposing of the streams.
                toAdapter.FinalizeItemWrite(toStream, updateInfo);

                encryptionManager?.Dispose();

                fromStream?.Close();
                toStream?.Close();
            }
        }

        private class TransferResult
        {
            public long BytesRead { get; set; }
            public long BytesWritten { get; set; }

            public byte[] Sha1Hash { get; set; }
            public byte[] Md5Hash { get; set; }

            public byte[] TransformedSha1Hash { get; set; }
            public byte[] TransformedMd5Hash { get; set; }
        }

        /// <summary>
        /// Transfer data from the source stream to the destination stream. This method also performs the necessary
        /// data manipulation (throttling, hashing, encrypting, etc) as the data is streamed. This allows a the 
        /// source stream to only be read a single time while performing multiple operations on the data.
        /// </summary>
        /// <param name="sourceStream">The stream that the data is read from</param>
        /// <param name="destinationStream">The stream that the data is written to</param>
        /// <param name="updateInfo">Metadata about the item being copied</param>
        /// <param name="throttlingManager">The throttling manager (to handling throttling, if required)</param>
        /// <param name="encryptionManager">The encryption manager (only used when encrypting/decrypting)</param>
        /// <returns>(async) The result of the transfer</returns>
        private async Task<TransferResult> TransferDataWithHashAsync(
            Stream sourceStream, 
            Stream destinationStream, 
            EntryUpdateInfo updateInfo,
            ThrottlingManager throttlingManager,
            EncryptionManager encryptionManager)
        {
            SHA1Cng sha1 = null;
            MD5Cng md5 = null;

            try
            {
                // By default, we will compute the SHA1 and MD5 hashes of the file as it is streamed.
                sha1 = new SHA1Cng();
                md5 = new MD5Cng();

                // Allocate the buffer that will be used to transfer the data. The source stream will be read one
                // suffer-size as a time, then written to the destination.
                byte[] buffer = new byte[transferBufferSize];
                int read;
                long readTotal = 0;
                long writtenTotal = 0;

                while (true)
                {
                    // If we are using a throttling manager, get the necessary number of tokens to
                    // transfer the data
                    if (throttlingManager != null)
                    {
                        int tokens = 0;
                        while (true)
                        {
                            // Get the number of tokens needed. We will require tokens equaling the number of
                            // bytes to be transferred. This is a non-blocking calling and will return between
                            // 0 and the number of requested tokens.
                            tokens += throttlingManager.GetTokens(transferBufferSize - tokens);
                            if (tokens >= transferBufferSize)
                            {
                                // We have enough tokens to transfer the buffer
                                break;
                            }

                            // We don't (yet) have enough tokens, so wait for a short duration and try again
                            await Task.Delay(10, this.cancellationTokenSource.Token).ConfigureAwait(false);
                        }
                    }

                    // Read data from the source adapter
                    read = sourceStream.Read(buffer, 0, buffer.Length);

                    // Increment the total number of bytes read from the source adapter
                    readTotal += read;

                    if (read <= buffer.Length)
                    {
                        // Compute the last part of the SHA1 and MD5 hashes (this finished the algorithm's work).
                        sha1.TransformFinalBlock(buffer, 0, read);
                        md5.TransformFinalBlock(buffer, 0, read);

                        if (encryptionManager != null)
                        {
                            writtenTotal += encryptionManager.TransformFinalBlock(buffer, 0, read);
                        }
                        else
                        {
                            destinationStream.Write(buffer, 0, read);
                            writtenTotal += buffer.Length;
                        }

                        // Increment the total number of bytes written to the desination adapter
                        this.bytesCompleted += read;

                        if (this.syncProgressUpdateStopwatch.ElapsedMilliseconds > 100)
                        {
                            this.RaiseSyncProgressChanged(updateInfo);
                            this.syncProgressUpdateStopwatch.Restart();
                        }

                        // Read the end of the stream
                        break;
                    }

                    // Pass the data through the required hashing algorithms.
                    sha1.TransformBlock(buffer, 0, read, buffer, 0);
                    md5.TransformBlock(buffer, 0, read, buffer, 0);

                    // Write the data to the destination adapter
                    if (encryptionManager != null)
                    {
                        writtenTotal += encryptionManager.TransformBlock(buffer, 0, read);
                    }
                    else
                    {
                        destinationStream.Write(buffer, 0, read);
                        writtenTotal += buffer.Length;
                    }

                    // Increment the total number of bytes written to the desination adapter
                    this.bytesCompleted += read;

                    if (this.syncProgressUpdateStopwatch.ElapsedMilliseconds > 100)
                    {
                        this.RaiseSyncProgressChanged(updateInfo);
                        this.syncProgressUpdateStopwatch.Restart();
                    }
                }

                TransferResult result = new TransferResult
                {
                    BytesRead = readTotal,
                    BytesWritten = writtenTotal
                };

                if (encryptionManager != null)
                {
                    if (encryptionManager.Mode == EncryptionMode.Encrypt)
                    {
                        result.Sha1Hash = sha1.Hash;
                        result.Md5Hash = md5.Hash;
                        result.TransformedSha1Hash = encryptionManager.Sha1Hash;
                        result.TransformedMd5Hash = encryptionManager.Md5Hash;
                    }
                    else
                    {
                        result.TransformedSha1Hash = sha1.Hash;
                        result.TransformedMd5Hash = md5.Hash;
                        result.Sha1Hash = encryptionManager.Sha1Hash; // The SHA1 hash of the data written by the encryption manager
                        result.Md5Hash = encryptionManager.Md5Hash;
                    }
                }
                else
                {
                    result.Sha1Hash = sha1.Hash;
                    result.Md5Hash = md5.Hash;
                }

                return result;
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