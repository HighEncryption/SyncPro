namespace SyncPro.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Data.Entity;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    using SyncPro.Adapters;
    using SyncPro.Configuration;
    using SyncPro.Data;
    using SyncPro.Tracing;

    public class AnalyzeJob : JobBase
    {
        private readonly AnalyzeRelationshipResult analyzeResult;

        public EventHandler<AnalyzeJobProgressInfo> ChangeDetected;

        public AnalyzeRelationshipResult AnalyzeResult => this.analyzeResult;

        public AnalyzeJob(SyncRelationship relationship)
            : base(relationship)
        {
            this.analyzeResult = new AnalyzeRelationshipResult();
        }

        public Task Start()
        {
            return this.StartTask();
        }

        protected override async Task ExecuteTask()
        {
            List<Task> updateTasks = new List<Task>();

            // For each adapter (where changes can origiante from), start a task to analyze the change for that
            // adapter. This will allow multiple adapters to be examined in parallel.
            foreach (AdapterBase adapter in this.Relationship.Adapters.Where(a => a.Configuration.IsOriginator))
            {
                this.analyzeResult.AdapterResults.Add(adapter.Configuration.Id, new AnalyzeAdapterResult());
                updateTasks.Add(this.AnalyzeChangesFromAdapter(adapter));
            }

            await Task.WhenAll(updateTasks).ContinueWith(task =>
            {
                if (updateTasks.All(t => t.IsCompleted))
                {
                    this.analyzeResult.IsComplete = true;
                }
            });

            this.CalculateUnchangedEntryCounts();
        }

        private void CalculateUnchangedEntryCounts()
        {
            Dictionary<long, SyncEntryType> changedEntries = new Dictionary<long, SyncEntryType>();

            foreach (AnalyzeAdapterResult adapterResult in this.analyzeResult.AdapterResults.Values)
            {
                foreach (EntryUpdateInfo entryUpdateInfo in adapterResult.EntryResults)
                {
                    // Any change results that indicate that the entry is New can be skipped, since we
                    // know that they dont exist in the DB yet.
                    if (entryUpdateInfo.HasSyncEntryFlag(SyncEntryChangedFlags.NewFile) ||
                        entryUpdateInfo.HasSyncEntryFlag(SyncEntryChangedFlags.NewDirectory))
                    {
                        continue;
                    }

                    try
                    {
                        changedEntries.Add(
                            entryUpdateInfo.Entry.Id,
                            entryUpdateInfo.Entry.Type);
                    }
                    catch (ArgumentException)
                    {
                        // A duplicate was added. Suppress.
                    }
                }
            }

            using (SyncDatabase db = this.Relationship.GetDatabase())
            {
                foreach (SyncEntry syncEntry in db.Entries)
                {
                    if (changedEntries.ContainsKey(syncEntry.Id))
                    {
                        continue;
                    }

                    switch (syncEntry.Type)
                    {
                        case SyncEntryType.Directory:
                            this.analyzeResult.UnchangedFolderCount++;
                            break;
                        case SyncEntryType.File:
                            this.analyzeResult.UnchangedFileCount++;
                            this.analyzeResult.UnchangedFileBytes += syncEntry.GetSize(this.Relationship, SyncEntryPropertyLocation.Source);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }
        }

        private async Task AnalyzeChangesFromAdapter(AdapterBase adapter)
        {
            Logger.AnalyzeChangesStart(
                new Dictionary<string, object>()
                {
                    { "SyncAnalysisRunId", adapter.Configuration.Id },
                    { "AdapterId", adapter.Configuration.Id },
                    { "SupportChangeTracking", adapter.SupportChangeTracking() }
                });

            try
            {
                using (SyncDatabase db = this.Relationship.GetDatabase())
                {
                    SyncEntry rootIndexEntry = db.Entries.FirstOrDefault(e => e.Id == adapter.Configuration.RootIndexEntryId);

                    if (adapter.SupportChangeTracking())
                    {
                        IChangeTracking changeTracking = (IChangeTracking) adapter;
                        TrackedChange trackedChange = await changeTracking.GetChangesAsync().ConfigureAwait(false);

                        this.analyzeResult.TrackedChanges.Add(adapter, trackedChange);
                        this.AnalyzeChangesWithChangeTracking(db, adapter, rootIndexEntry);
                    }
                    else
                    {
                        IAdapterItem rootFolder = adapter.GetRootFolder().Result;

                        this.AnalyzeChangesWithoutChangeTracking(
                            db,
                            adapter,
                            rootFolder,
                            rootIndexEntry,
                            string.Empty);
                    }
                }
            }
            catch (Exception exception)
            {
                this.analyzeResult.AdapterResults[adapter.Configuration.Id].Exception = exception;
            }
            finally
            {
                Logger.AnalyzeChangesEnd(
                    new Dictionary<string, object>()
                    {
                        { "ResultId", this.analyzeResult.Id },
                        { "AdapterId", adapter.Configuration.Id },
                        { "IsUpToDate", this.analyzeResult.IsUpToDate },
                        { "TotalSyncEntries", this.analyzeResult.TotalSyncEntries },
                        { "TotalChangedEntriesCount", this.analyzeResult.TotalChangedEntriesCount },
                    });
            }
        }

        private void AnalyzeChangesWithChangeTracking(
            SyncDatabase db,
            AdapterBase adapter,
            SyncEntry logicalParent)
        {
            TrackedChange trackedChange = this.analyzeResult.TrackedChanges[adapter];

            Logger.Debug(
                Logger.BuildEventMessageWithProperties(
                    "AnalyzeChangesWithChangeTracking called with following properties:",
                    new Dictionary<string, object>()
                    {
                        { "ResultId", this.analyzeResult.Id },
                        { "AdapterId", adapter.Configuration.Id },
                        { "RootName", logicalParent.Name },
                        { "RootId", logicalParent.Id },
                        { "TrackedChangeState", trackedChange.State },
                        { "TrackedChangeCount", trackedChange.Changes.Count },
                    }));

            // When analyzing changes with change tracking, it is possible that changes arrive out of order (such as a
            // file creation arriving before the file's parent directory creation), which results in a change not being
            // able to be processed. If a change cannot be processed, it is 'skipped' by adding it back to the end of 
            // the queue of changes.
            Queue<IChangeTrackedAdapterItem> pendingChanges = new Queue<IChangeTrackedAdapterItem>(trackedChange.Changes);

            // TODO: This is a perfect candidate for an event message that should use activity GUIDs.
            Logger.Debug("pendingChanges contains {0} items", pendingChanges.Count);

            // This field tracks the number of changes that are skipped. If the number of skipped changes exceeds the 
            // total number of changes to be analyzed, throw an exception.
            int skipCount = 0;
            Dictionary<string, SyncEntry> knownSyncEntries = new Dictionary<string, SyncEntry>();

            while (pendingChanges.Any() && !this.CancellationToken.IsCancellationRequested)
            {
                IChangeTrackedAdapterItem changeAdapterItem = pendingChanges.Dequeue();

                if (this.AnalyzeSingleChangeWithTracking(db, adapter, changeAdapterItem, knownSyncEntries))
                {
                    // Change change was successfully analyzed. Reset the skip counter.
                    skipCount = 0;
                }
                else
                {
                    skipCount++;
                    pendingChanges.Enqueue(changeAdapterItem);
                }

                if (skipCount > pendingChanges.Count)
                {
                    throw new Exception("Analysis failure!");
                }
            }
        }

        private bool AnalyzeSingleChangeWithTracking(
            SyncDatabase db, 
            AdapterBase adapter, 
            IChangeTrackedAdapterItem changeAdapterItem, 
            Dictionary<string, SyncEntry> knownSyncEntries)
        {
            // Get the logical item for this change
            SyncEntryAdapterData adapterEntry =
                db.AdapterEntries.Include(e => e.SyncEntry).FirstOrDefault(e => e.AdapterEntryId == changeAdapterItem.UniqueId);

            if (changeAdapterItem.IsDeleted)
            {
                // It is possible that an item was created and deleted before a sync cycle was run, so we need to handle the 
                // case when the adapterEntry is not present.
                if (adapterEntry == null)
                {
#if DEBUG
                    Debugger.Break(); // This should almost never happen!
#endif

                    Logger.Debug("Skipping delete for non-existent item " + changeAdapterItem.Name);
                    return true;
                }

                SyncEntry logicalChild = adapterEntry.SyncEntry;
                Logger.Debug("Child item {0} ({1}) was deleted.", logicalChild.Id, logicalChild.Name);

                // Mark the sync entry as 'deleted' by setting the appropriate bit.
                logicalChild.State |= SyncEntryState.IsDeleted;

                // Set the state to 'Unsynchronized' (so that we know we have pending changes).
                logicalChild.State |= SyncEntryState.NotSynchronized;

                // Create the update info for the new entry
                logicalChild.UpdateInfo = new EntryUpdateInfo(
                    logicalChild,
                    adapter,
                    SyncEntryChangedFlags.Deleted,
                    logicalChild.GetRelativePath(db, "/"));

                // Set all previous metadata values from the values in the sync entry. Since this is a 
                // deletion, all of the previous metadata values will be populated and the current values
                // will be null/empty.
                logicalChild.UpdateInfo.SetOldMetadataFromSyncEntry();

                this.RaiseChangeDetected(adapter.Configuration.Id, logicalChild.UpdateInfo);

                this.LogSyncAnalyzerChangeFound(logicalChild);
                return true;
            }

            if (adapterEntry != null)
            {
                // The item was found in the database
                SyncEntry logicalChild = adapterEntry.SyncEntry;

                if (!knownSyncEntries.ContainsKey(adapterEntry.AdapterEntryId))
                {
                    knownSyncEntries.Add(adapterEntry.AdapterEntryId, logicalChild);
                }

                Logger.Debug("Found child item {0} in database that matches adapter item.", logicalChild.Id);

                if (logicalChild.State.HasFlag(SyncEntryState.IsDeleted))
                {
                    // Handle the case where the item was previously deleted
                    // TODO: verify that this is correct. Remove this comment once a unit test is added.
                    Logger.Debug("Child item {0} was un-deleted.", logicalChild.Id);

                    // Clear the IsDeleted flag (needed in cases where the file was previously deleted).
                    logicalChild.State &= ~SyncEntryState.IsDeleted;

                    // Create the update info for the new entry (marked as a new file/directory)
                    logicalChild.UpdateInfo = new EntryUpdateInfo(
                        logicalChild,
                        adapter, 
                        SyncEntryChangedFlags.Restored, 
                        logicalChild.GetRelativePath(db, "/"));

                    // Because the item was previously deleted, all of the previous values will be null/empty.
                    // Other metadata will need to be set for new properties
                    logicalChild.UpdateInfo.CreationDateTimeUtcNew = changeAdapterItem.CreationTimeUtc;
                    logicalChild.UpdateInfo.ModifiedDateTimeUtcNew = changeAdapterItem.ModifiedTimeUtc;

                    // Raise change notification so that the UI can be updated in "real time" rather than waiting for
                    // the analyze process to finish.
                    this.RaiseChangeDetected(adapter.Configuration.Id, logicalChild.UpdateInfo);

                    this.LogSyncAnalyzerChangeFound(logicalChild);
                    return true;
                }

                EntryUpdateResult updateResult;

                // If the item differs from the entry in the index, an update will be required.
                if (logicalChild.UpdateInfo == null && 
                    adapter.IsEntryUpdated(logicalChild, changeAdapterItem, out updateResult))
                {
                    Logger.Debug("Child item {0} is out of sync.", logicalChild.Id);

                    // Create the update info for the new entry
                    logicalChild.UpdateInfo = new EntryUpdateInfo(
                        logicalChild, 
                        adapter, 
                        updateResult.ChangeFlags,
                        logicalChild.GetRelativePath(db, "/"));

                    // Set all of the previous metadata values to those from the sync entry
                    logicalChild.UpdateInfo.SetOldMetadataFromSyncEntry();

                    // Set the new timestamps according to what the adapter returns
                    if (logicalChild.UpdateInfo.CreationDateTimeUtcOld != changeAdapterItem.CreationTimeUtc)
                    {
                        logicalChild.UpdateInfo.CreationDateTimeUtcNew = changeAdapterItem.CreationTimeUtc;
                    }

                    if (logicalChild.UpdateInfo.ModifiedDateTimeUtcOld != changeAdapterItem.ModifiedTimeUtc)
                    {
                        logicalChild.UpdateInfo.ModifiedDateTimeUtcNew = changeAdapterItem.ModifiedTimeUtc;
                    }

                    if (string.CompareOrdinal(logicalChild.UpdateInfo.RelativePath, logicalChild.UpdateInfo.PathOld) != 0)
                    {
                        logicalChild.UpdateInfo.PathNew = logicalChild.UpdateInfo.RelativePath;
                    }

                    // Raise change notification so that the UI can be updated in "real time" rather than waiting for 
                    // the analyze process to finish.
                    this.RaiseChangeDetected(adapter.Configuration.Id, logicalChild.UpdateInfo);

                    this.LogSyncAnalyzerChangeFound(logicalChild);
                    return true;
                }

                // The change tracking indicates that this item has changed, but the adapter logic determined that it 
                // does not.
                Logger.Debug("Child item {0} is already synced.", logicalChild.Id);
                return true;
            }
            else
            {
                Logger.Debug("Child item {0} ({1}) was not found in database that matches adapter item.", changeAdapterItem.Name,
                    changeAdapterItem.UniqueId);

                SyncEntry parentEntry;
                if (!knownSyncEntries.TryGetValue(changeAdapterItem.ParentUniqueId, out parentEntry))
                {
                    SyncEntryAdapterData parentAdapterEntry =
                        db.AdapterEntries.Include(e => e.SyncEntry).FirstOrDefault(e => e.AdapterEntryId.Equals(changeAdapterItem.ParentUniqueId));

                    if (parentAdapterEntry != null)
                    {
                        parentEntry = parentAdapterEntry.SyncEntry;
                    }
                }

                if (parentEntry == null)
                {
                    Logger.Debug("Delaying analyze for parent.");
                    return false;
                }

                // This file/directory was not found in the index, so create a new entry for it. Note that while this is a call
                // to the adapter, no object is created as a result of the call (the result is in-memory only).
                SyncEntry logicalChild = adapter.CreateSyncEntryForAdapterItem(changeAdapterItem, parentEntry);

                knownSyncEntries.Add(changeAdapterItem.UniqueId, logicalChild);

                // Set the NotSynchronized flag so that we know this has not yet been committed to the database.
                logicalChild.State = SyncEntryState.NotSynchronized;

                // Create the update info for the new entry
                logicalChild.UpdateInfo = new EntryUpdateInfo(
                    logicalChild,
                    adapter,
                    GetFlagsForNewSyncEntry(changeAdapterItem),
                    logicalChild.GetRelativePath(db, "/"));

                logicalChild.UpdateInfo.CreationDateTimeUtcNew = changeAdapterItem.CreationTimeUtc;
                logicalChild.UpdateInfo.ModifiedDateTimeUtcNew = changeAdapterItem.ModifiedTimeUtc;
                logicalChild.UpdateInfo.PathNew = logicalChild.UpdateInfo.RelativePath;

                // Some providers can return information that normally isn't known until the item 
                // is copied. Copy the values to the UpdateInfo object.
                if (this.Relationship.EncryptionMode == EncryptionMode.Decrypt)
                {
                    logicalChild.UpdateInfo.EncryptedSizeNew = changeAdapterItem.Size;
                    logicalChild.UpdateInfo.EncryptedSha1HashNew = changeAdapterItem.Sha1Hash;
                }
                else
                {
                    logicalChild.UpdateInfo.OriginalSizeNew = changeAdapterItem.Size;
                    logicalChild.UpdateInfo.OriginalSha1HashNew = changeAdapterItem.Sha1Hash;
                }

                // Raise change notification so that the UI can be updated in "real time" rather than waiting for the analyze process to finish.
                this.RaiseChangeDetected(adapter.Configuration.Id, logicalChild.UpdateInfo);

                this.LogSyncAnalyzerChangeFound(logicalChild);
                return true;
            }
        }

        private void LogSyncAnalyzerChangeFound(SyncEntry logicalChild)
        {
            string message;
            if (logicalChild.UpdateInfo.HasSyncEntryFlag(SyncEntryChangedFlags.Deleted))
            {
                message = "The item was deleted";
            }
            else if (logicalChild.UpdateInfo.HasSyncEntryFlag(SyncEntryChangedFlags.IsNew))
            {
                message = "A new item was found";
            }
            else if (logicalChild.UpdateInfo.HasSyncEntryFlag(SyncEntryChangedFlags.IsUpdated))
            {
                message = "The item was updated";
            }
            else
            {
                message = "An unknown change has occurred in the item";
            }

            Logger.SyncAnalyzerChangeFound(
                Logger.BuildEventMessageWithProperties(
                    message,
                    new Dictionary<string, object>()
                    {
                        { "ResultId", this.analyzeResult.Id },
                        { "Id", logicalChild.Id },
                        { "Name", logicalChild.Name },
                        { "Flags", logicalChild.UpdateInfo.GetSetFlagNames() },
                    }));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="db"></param>
        /// <param name="adapter"></param>
        /// <param name="adapterParent"></param>
        /// <param name="logicalParent"></param>
        /// <param name="relativePath"></param>
        /// <remarks>
        /// This method uses a recursive strategy for comparing items exposed by an adapter. This requires retrieving the metadata
        /// for each item on the adapter (expensive for non-local adapters).
        /// </remarks>
        private void AnalyzeChangesWithoutChangeTracking(
            SyncDatabase db,
            AdapterBase adapter,
            IAdapterItem adapterParent,
            SyncEntry logicalParent,
            string relativePath)
        {
            IList<IAdapterItem> adapterChildren = new List<IAdapterItem>();

            Logger.Debug(
                Logger.BuildEventMessageWithProperties(
                    "AnalyzeChangesWithoutChangeTracking called with following properties:",
                    new Dictionary<string, object>()
                    {
                        { "ResultId", this.analyzeResult.Id },
                        { "AdapterId", adapter.Configuration.Id },
                        { "RootName", logicalParent.Name },
                        { "RootId", logicalParent.Id },
                    }));

            // Get files and folders in the given directory. If this given directory is null (such as when a directory was deleted),
            // the list will be empty (allowing deletes to occur recursivly).
            if (adapterParent != null)
            {
                Logger.Debug("Getting child adapter items for item {0}", adapterParent.FullName);
                adapterChildren = adapter.GetAdapterItems(adapterParent).ToList();
            }

            Logger.Debug("Found {0} child items from adapter.", adapterChildren.Count);

            // Get the children (files and directories) for a particular directory as identified by 'entry'.
            // Performance Note: Check if the logical parent is a new item. If so, then any of the children will
            // be new as well, so don't query the DB for them. The logic below determines if we need to make this
            // DB query inverted, as it is easier to read by a human.
            bool skipChildLookup =
                logicalParent.State == SyncEntryState.NotSynchronized &&
                logicalParent.UpdateInfo != null &&
                logicalParent.UpdateInfo.HasSyncEntryFlag(SyncEntryChangedFlags.IsNew);

            List<SyncEntry> logicalChildren = null;

            if (!skipChildLookup)
            {
                logicalChildren = db.Entries.Include(e => e.AdapterEntries).Where(e => e.ParentId == logicalParent.Id).ToList();
                Logger.Debug("Found {0} child items from database.", logicalChildren.Count);
            }
            else
            {
                Logger.Debug("Skipped child lookup from database.");
            }

            // Loop through each of the items return by the adapter (eg files on disk)
            foreach (IAdapterItem adapterChild in adapterChildren)
            {
                if (this.CancellationToken.IsCancellationRequested)
                {
                    break;
                }

                // TODO: Query the adapter somehow to see if the file should be suppressed (sparse file, for example).
                // TODO: Further note - this would be the right place to implement an inclusion/exclusion behavior.

                // Check if there was an error reading the item from the adapter. If so, skip the item.
                if (!string.IsNullOrEmpty(adapterChild.ErrorMessage))
                {
                    Logger.Info("Skipping adapter child '{0}' with error.", adapterChild.FullName);
                    continue;
                }

                // Get the list of sync entries from the index. We will match these up with the adapter items to determine what has been 
                // added, modified, or removed.
                Logger.Debug("Examining adapter child item {0}", adapterChild.FullName);

                // First check if there is an entry that matches the unique ID of the item
                SyncEntry logicalChild = logicalChildren?.FirstOrDefault(c => c.HasUniqueId(adapterChild.UniqueId));

                // A match was found, so determine
                if (logicalChild != null)
                {
                    Logger.Debug("Found child item {0} in database that matches adapter item.", logicalChild.Id);

                    // The database already contains an entry for this item. Remove it from the list, then update as needed.
                    logicalChildren.Remove(logicalChild);

                    if (logicalChild.State.HasFlag(SyncEntryState.IsDeleted))
                    {
                        Logger.Debug("Child item {0} was un-deleted.", logicalChild.Id);

                        // Clear the IsDeleted flag (needed in cases where the file was previously deleted).
                        logicalChild.State &= ~SyncEntryState.IsDeleted;

                        // Create the update info for the new entry (marked as a new file/directory)
                        logicalChild.UpdateInfo = new EntryUpdateInfo(
                            logicalChild,
                            adapter,
                            SyncEntryChangedFlags.Restored,
                            Path.Combine(relativePath, logicalChild.Name));

                        // Because the item was previously deleted, all of the previous values will be null/empty
                        // Other metadata will need to be set for new properties
                        logicalChild.UpdateInfo.CreationDateTimeUtcNew = adapterChild.CreationTimeUtc;
                        logicalChild.UpdateInfo.ModifiedDateTimeUtcNew = adapterChild.ModifiedTimeUtc;

                        // Raise change notification so that the UI can be updated in "real time" rather than waiting for the analyze 
                        // process to finish.
                        this.RaiseChangeDetected(adapter.Configuration.Id, logicalChild.UpdateInfo);

                        this.LogSyncAnalyzerChangeFound(logicalChild);
                        continue;
                    }

                    EntryUpdateResult updateResult;

                    // If the item differs from the entry in the index, an update will be required.
                    if (logicalChild.UpdateInfo == null && adapter.IsEntryUpdated(logicalChild, adapterChild, out updateResult))
                    {
                        Logger.Debug("Child item {0} is out of sync.", logicalChild.Id);

                        // Create the update info for the new entry
                        logicalChild.UpdateInfo = new EntryUpdateInfo(
                            logicalChild, 
                            adapter,
                            updateResult.ChangeFlags, 
                            Path.Combine(relativePath, logicalChild.Name));

                        // Set all of the previous metadata values to those from the sync entry
                        logicalChild.UpdateInfo.SetOldMetadataFromSyncEntry();

                        // Set the new timestamps according to what the adapter returns
                        if (logicalChild.UpdateInfo.CreationDateTimeUtcOld != adapterChild.CreationTimeUtc)
                        {
                            logicalChild.UpdateInfo.CreationDateTimeUtcNew = adapterChild.CreationTimeUtc;
                        }

                        if (logicalChild.UpdateInfo.ModifiedDateTimeUtcOld != adapterChild.ModifiedTimeUtc)
                        {
                            logicalChild.UpdateInfo.ModifiedDateTimeUtcNew = adapterChild.ModifiedTimeUtc;
                        }

                        if (string.CompareOrdinal(logicalChild.UpdateInfo.RelativePath, logicalChild.UpdateInfo.PathOld) != 0)
                        {
                            logicalChild.UpdateInfo.PathNew = logicalChild.UpdateInfo.RelativePath;
                        }

                        if (this.Relationship.EncryptionMode == EncryptionMode.Decrypt)
                        {
                            if (logicalChild.UpdateInfo.EncryptedSizeOld != adapterChild.Size)
                            {
                                logicalChild.UpdateInfo.EncryptedSizeNew = adapterChild.Size;
                            }
                        }
                        else
                        {
                            if (logicalChild.UpdateInfo.OriginalSizeOld != adapterChild.Size)
                            {
                                logicalChild.UpdateInfo.OriginalSizeNew = adapterChild.Size;
                            }
                        }

                        // Set the NotSynchronized flag so that we know this has not yet been committed to the database.
                        logicalChild.State = SyncEntryState.NotSynchronized;

                        // Raise change notification so that the UI can be updated in "real time" rather than waiting for the analyze process to finish.
                        this.RaiseChangeDetected(adapter.Configuration.Id, logicalChild.UpdateInfo);
                        this.LogSyncAnalyzerChangeFound(logicalChild);
                    }
                }
                else
                {
                    Logger.Debug("Child item was not found in database that matches adapter item.");

                    // This file/directory was not found in the index, so create a new entry for it. Note that while this is a call
                    // to the adapter, no object is created as a result of the call (the result is in-memory only).
                    logicalChild = adapter.CreateSyncEntryForAdapterItem(adapterChild, logicalParent);

                    // Set the NotSynchronized flag so that we know this has not yet been committed to the database.
                    logicalChild.State = SyncEntryState.NotSynchronized;

                    // Create the update info for the new entry
                    logicalChild.UpdateInfo = new EntryUpdateInfo(
                        logicalChild,
                        adapter,
                        GetFlagsForNewSyncEntry(adapterChild),
                        Path.Combine(relativePath, logicalChild.Name));

                    logicalChild.UpdateInfo.CreationDateTimeUtcNew = adapterChild.CreationTimeUtc;
                    logicalChild.UpdateInfo.ModifiedDateTimeUtcNew = adapterChild.ModifiedTimeUtc;
                    logicalChild.UpdateInfo.PathNew = logicalChild.UpdateInfo.RelativePath;

                    // Raise change notification so that the UI can be updated in "real time" rather than waiting for the analyze process to finish.
                    this.RaiseChangeDetected(adapter.Configuration.Id, logicalChild.UpdateInfo);
                    this.LogSyncAnalyzerChangeFound(logicalChild);
                }

                // If this is a directory, descend into it
                if (adapterChild.ItemType == SyncAdapterItemType.Directory)
                {
                    this.AnalyzeChangesWithoutChangeTracking(
                        db,
                        adapter,
                        adapterChild,
                        logicalChild,
                        Path.Combine(relativePath, logicalChild.Name));
                }
            }

            // Any entries still in the indexChildren list are no longer present in the source adapter and
            // need to be expunged (recursivly).
            if (logicalChildren != null)
            {
                foreach (
                    SyncEntry oldChild in logicalChildren.Where(e => e.State.HasFlag(SyncEntryState.IsDeleted) == false))
                {
                    if (this.CancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    Logger.Debug("Child item {0} ({1}) was deleted.", oldChild.Id, oldChild.Name);

                    // Mark the sync entry as 'deleted' by setting the appropriate bit.
                    oldChild.State |= SyncEntryState.IsDeleted;

                    // Set the state to 'Unsynchronized' (so that we know we have pending changes).
                    oldChild.State |= SyncEntryState.NotSynchronized;

                    // Create the update info for the new entry
                    oldChild.UpdateInfo = new EntryUpdateInfo(
                        oldChild,
                        adapter,
                        SyncEntryChangedFlags.Deleted,
                        Path.Combine(relativePath, oldChild.Name));

                    // Set all previous metadata values from the values in the sync entry. Since this is a 
                    // deletion, all of the previous metadata values will be populated and the current values
                    // will be null/empty.
                    oldChild.UpdateInfo.SetOldMetadataFromSyncEntry();

                    this.RaiseChangeDetected(adapter.Configuration.Id, oldChild.UpdateInfo);
                    this.LogSyncAnalyzerChangeFound(oldChild);

                    // If the entry we are removing is a directory, we need to also check for subdirectories and files. We can do this by calling 
                    // this method recursivly, and passing null for adapter folder (indicating that is no longer exists according to the 
                    // originating adapter.
                    if (oldChild.Type == SyncEntryType.Directory)
                    {
                        this.AnalyzeChangesWithoutChangeTracking(
                            db,
                            adapter,
                            null,
                            oldChild,
                            Path.Combine(relativePath, oldChild.Name));
                    }
                }
            }
        }

        private static SyncEntryChangedFlags GetFlagsForNewSyncEntry(IAdapterItem item)
        {
            if (item.ItemType == SyncAdapterItemType.Directory)
            {
                return SyncEntryChangedFlags.NewDirectory;
            }

            if (item.ItemType == SyncAdapterItemType.File)
            {
                return SyncEntryChangedFlags.NewFile;
            }

            throw new InvalidOperationException("Cannot create flags for unknown item type.");
        }

        private void RaiseChangeDetected(int adapterId, EntryUpdateInfo updateInfo)
        {
            this.analyzeResult.AdapterResults[adapterId].EntryResults.Add(updateInfo);
            this.ChangeDetected?.Invoke(
                this, 
                new AnalyzeJobProgressInfo(
                    updateInfo, 
                    this.analyzeResult.AdapterResults[adapterId].EntryResults.Count,
                    0));
        }
    }
}