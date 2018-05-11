namespace SyncPro.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Data.Entity;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using SyncPro.Adapters;
    using SyncPro.Configuration;
    using SyncPro.Data;
    using SyncPro.Tracing;
    using SyncPro.Utility;

    /// <summary>
    /// Contains the logic for analyzing changes in a relationship
    /// </summary>
    /// <remarks>
    /// The AnalyzeJob class comprises one of the two central components in the process of
    /// synchronizing files (the other being the SyncJob class).
    /// </remarks>
    public class AnalyzeJobWorker
    {
        private readonly SyncRelationship relationship;
        private readonly AdapterBase sourceAdapter;
        private readonly AdapterBase destAdapter;
        private readonly AnalyzeRelationshipResult analyzeRelationshipResult;
        private readonly CancellationToken cancellationToken;

        // Local instance of the database
        private SyncDatabase db;

        // The entry for the root of the file/folder tree
        private SyncEntry rootIndexEntry;

        // Indicates if the relationship has completed an initial sync
        private bool firstSyncComplete;

        // This array is used by the recursive analysis logic to find files that have been moved
        // from one directory to another on the source adapter. A moved file will first be detected
        // as either a new item or a deleted item, depending on whether the new location of the 
        // item is analyzed before the old location.
        // When a new item is found (an item that was not previously known to exist that directory),
        // that item's adapter ID is checked against the list of known adapter item IDs from the 
        // array below. If a match is found, the DB is directly queried to find the actual item's
        // information, and this item can then be recorded as a move.
        // If a match is not found, then this is a new file/directory and will be recorded as such.
        private int[] adapterEntryHashList;

        // Contains the uniqueIds for items that are known to have been moved (as opposed to having
        // been added/modified/deleted). Moving items is typically more infrequent that adding or
        // updating items, so we will keep the list of moved items to greatly improve performance
        // when detecting that an item has moved.
        private List<string> movedEntries;

        // This dictionary maintains a a list of tuples in the form of {UniqueId,UpdateInfo} for 
        // each entry that was deleted. This is needed in order to properly detect when an item
        // was moved from one folder to another, and the latter folder has not yet been analyzed, 
        // so that the entry is not incorrectly reported as having been deleted.
        private Dictionary<string, EntryUpdateInfo> deletedEntries;

        /// <summary>
        /// Invoked when a new change is detected during analysis.
        /// </summary>
        public EventHandler<AnalyzeJobProgressInfo> ChangeDetected;

        /// <summary>
        /// Contains the results of analysis
        /// </summary>
        public AnalyzeAdapterResult AnalyzeResult { get; }

        /// <summary>
        /// Create a new analyze worker
        /// </summary>
        /// <param name="relationship">The relationship to be analyzed</param>
        /// <param name="sourceAdapter">The adapter to examine where changed will be taken from</param>
        /// <param name="destAdapter">The adapter where new changes should be written to (during a Sync job).</param>
        /// <param name="analyzeRelationshipResult"></param>
        /// <param name="cancellationToken">The cancellation token</param>
        public AnalyzeJobWorker(SyncRelationship relationship,
            AdapterBase sourceAdapter,
            AdapterBase destAdapter,
            AnalyzeRelationshipResult analyzeRelationshipResult,
            CancellationToken cancellationToken)
        {
            this.relationship = relationship;
            this.sourceAdapter = sourceAdapter;
            this.destAdapter = destAdapter;
            this.analyzeRelationshipResult = analyzeRelationshipResult;
            this.cancellationToken = cancellationToken;

            this.AnalyzeResult = new AnalyzeAdapterResult();

            this.analyzeRelationshipResult.AdapterResults.Add(
                sourceAdapter.Configuration.Id, 
                this.AnalyzeResult);
        }

        /// <summary>
        /// Analyze the source adapters for any changes.
        /// </summary>
        public async Task AnalyzeChangesAsync()
        {
            Logger.AnalyzeChangesStart(
                new Dictionary<string, object>()
                {
                    { "SyncAnalysisRunId", this.analyzeRelationshipResult.Id },
                    { "AdapterId", sourceAdapter.Configuration.Id },
                    { "SupportChangeTracking", sourceAdapter.SupportChangeTracking() }
                });

            try
            {
                // Get the db instance that we will use for the duration of the analysis
                this.db = this.relationship.GetDatabase();

                this.firstSyncComplete = db.History.Any(h => h.Result == JobResult.Success);

                // Get the entry that represents the root of the tree from the db
                this.rootIndexEntry = db.Entries.FirstOrDefault(e => e.Id == sourceAdapter.Configuration.RootIndexEntryId);

                // The analysis uses a different strategy depending on the whether the source adapter (where the changes
                // originate from) supports change tracking, which would provide us with an easier-to-use set of changes
                // that will need to be applied. Without change tracking, we will need to perform a manual walk of the
                // file/directory tree and compare to what is in the database.
                if (sourceAdapter.SupportChangeTracking())
                {
                    // Get a reference to the source adapter with change tracking
                    IChangeTracking changeTracking = (IChangeTracking) sourceAdapter;

                    // Get the tracked changes from the adapter
                    TrackedChange trackedChange = await changeTracking.GetChangesAsync().ConfigureAwait(false);

                    this.AnalyzeResult.TrackedChanges = trackedChange;

                    // Perform the internal analysis on the changes to determine what/how to apply the changes
                    this.AnalyzeChangesWithChangeTracking();
                }
                else
                {
                    // Get the IAdapterItem for the root of the sync tree from the source adapter (where the
                    // changes will be originating from).
                    IAdapterItem sourceRootItem = await sourceAdapter.GetRootFolder();

                    // Get the IAdapterItem for the root of the sync tree from the destination adapter (where
                    // that changes will be copied to by the sync job). In the case of the initial sync, we 
                    // will attempt to walk the destination tree in lock-step with the source tree and look 
                    // for any files that already exist in the destination. If files from the source are 
                    // already present in the destination (and are identical), we can skip the copying of the
                    // file an instead add the file information directly to the database.
                    IAdapterItem destRootItem = await destAdapter.GetRootFolder();

                    // Create an array of known adapter item hashes. This will be the result of calling
                    // GetHashCode() for each item's adapter ID (string). See comments on the field's 
                    // declaration for more info.
                    this.adapterEntryHashList = this.BuildAdapterItemHashList();

                    // Initialize the list of entries that are detected as moved or deleted items
                    this.movedEntries = new List<string>();
                    this.deletedEntries = new Dictionary<string, EntryUpdateInfo>();

                    // Build the list of changes by recursively walking the directory structure. This method
                    // is where the bulk of the analysis work will be performed.
                    this.AnalyzeChangesWithoutChangeTracking(
                        sourceRootItem,
                        destRootItem,
                        this.rootIndexEntry,
                        string.Empty);

                    // Any items that were detected as being deleted will be in the deletedEntries list. Now 
                    // that the full analysis is complete, raise the change notification for these entries.
                    foreach (KeyValuePair<string, EntryUpdateInfo> deletedEntry in this.deletedEntries)
                    {
                        this.RaiseChangeDetected(sourceAdapter.Configuration.Id, deletedEntry.Value);
                        this.LogSyncAnalyzerChangeFound(deletedEntry.Value.Entry);
                    }
                }

                // Now that the raw analysis for this set of adapters is complete, calculate the number
                // of files that have not been changed.
                this.CalculateUnchangedEntryCounts();
            }
            catch (Exception exception)
            {
                this.AnalyzeResult.Exception = exception;
            }
            finally
            {
                if (this.db != null)
                {
                    this.db.Dispose();
                    this.db = null;
                }

                Logger.AnalyzeChangesEnd(
                    new Dictionary<string, object>()
                    {
                        { "SyncAnalysisRunId", this.analyzeRelationshipResult.Id },
                        { "AdapterId", sourceAdapter.Configuration.Id },
                        { "IsUpToDate", !this.AnalyzeResult.EntryResults.Any() },
                        { "TotalSyncEntries", this.AnalyzeResult.EntryResults.Count },
                        { "TotalChangedEntriesCount", this.analyzeRelationshipResult.TotalChangedEntriesCount },
                    });
            }
        }

        private void CalculateUnchangedEntryCounts()
        {
            Dictionary<long, SyncEntryType> changedEntries = new Dictionary<long, SyncEntryType>();

            foreach (EntryUpdateInfo entryUpdateInfo in this.AnalyzeResult.EntryResults)
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

            foreach (SyncEntry syncEntry in db.Entries)
            {
                if (changedEntries.ContainsKey(syncEntry.Id))
                {
                    continue;
                }

                switch (syncEntry.Type)
                {
                    case SyncEntryType.Directory:
                        this.AnalyzeResult.UnchangedFolderCount++;
                        break;
                    case SyncEntryType.File:
                        this.AnalyzeResult.UnchangedFileCount++;
                        this.AnalyzeResult.UnchangedFileBytes +=
                            syncEntry.GetSize(this.relationship, SyncEntryPropertyLocation.Source);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        /// <summary>
        /// Creates the list of hash codes for unique Ids for existing items.
        /// </summary>
        /// <returns>The array of hash codes</returns>
        /// <remarks>
        /// See the comments on the adapterEntryHashList field for more information.
        /// </remarks>
        private int[] BuildAdapterItemHashList()
        {
            int[] hashList = new int[db.AdapterEntries.Count()];

            int i = 0;
            foreach (var entry in db.AdapterEntries.Where(e => e.AdapterId == sourceAdapter.Configuration.Id))
            {
                hashList[i] = entry.AdapterEntryId.GetHashCode();
                i++;
            }

            return hashList;
        }

        private void AnalyzeChangesWithChangeTracking()
        {
            TrackedChange trackedChange = this.AnalyzeResult.TrackedChanges;

            Logger.Debug(
                Logger.BuildEventMessageWithProperties(
                    "AnalyzeChangesWithChangeTracking called with following properties:",
                    new Dictionary<string, object>()
                    {
                        { "SyncAnalysisRunId", this.analyzeRelationshipResult.Id },
                        { "AdapterId", sourceAdapter.Configuration.Id },
                        { "RootName", rootIndexEntry.Name },
                        { "RootId", rootIndexEntry.Id },
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

            while (pendingChanges.Any() && !this.cancellationToken.IsCancellationRequested)
            {
                IChangeTrackedAdapterItem changeAdapterItem = pendingChanges.Dequeue();

                if (this.AnalyzeSingleChangeWithTracking(changeAdapterItem, knownSyncEntries))
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
                    this.sourceAdapter,
                    SyncEntryChangedFlags.Deleted,
                    logicalChild.GetRelativePath(db, "/"));

                // Set all previous metadata values from the values in the sync entry. Since this is a 
                // deletion, all of the previous metadata values will be populated and the current values
                // will be null/empty.
                logicalChild.UpdateInfo.SetOldMetadataFromSyncEntry();

                this.RaiseChangeDetected(this.sourceAdapter.Configuration.Id, logicalChild.UpdateInfo);

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
                        this.sourceAdapter, 
                        SyncEntryChangedFlags.Restored, 
                        logicalChild.GetRelativePath(db, "/"));

                    // Because the item was previously deleted, all of the previous values will be null/empty.
                    // Other metadata will need to be set for new properties
                    logicalChild.UpdateInfo.CreationDateTimeUtcNew = changeAdapterItem.CreationTimeUtc;
                    logicalChild.UpdateInfo.ModifiedDateTimeUtcNew = changeAdapterItem.ModifiedTimeUtc;

                    // Raise change notification so that the UI can be updated in "real time" rather than waiting for
                    // the analyze process to finish.
                    this.RaiseChangeDetected(this.sourceAdapter.Configuration.Id, logicalChild.UpdateInfo);

                    this.LogSyncAnalyzerChangeFound(logicalChild);
                    return true;
                }

                EntryUpdateResult updateResult;

                // If the item differs from the entry in the index, an update will be required.
                if (logicalChild.UpdateInfo == null && 
                    this.sourceAdapter.IsEntryUpdated(logicalChild, changeAdapterItem, out updateResult))
                {
                    Logger.Debug("Child item {0} is out of sync.", logicalChild.Id);

                    // Create the update info for the new entry
                    logicalChild.UpdateInfo = new EntryUpdateInfo(
                        logicalChild, 
                        this.sourceAdapter, 
                        updateResult.ChangeFlags,
                        logicalChild.GetRelativePath(db, "/"));

                    // Set all of the previous metadata values to those from the sync entry
                    logicalChild.UpdateInfo.SetNewMetadataFromSyncEntry();

                    // Set the new timestamps according to what the adapter returns
                    if (logicalChild.UpdateInfo.CreationDateTimeUtcNew != changeAdapterItem.CreationTimeUtc)
                    {
                        logicalChild.UpdateInfo.CreationDateTimeUtcOld = changeAdapterItem.CreationTimeUtc;
                    }

                    if (logicalChild.UpdateInfo.ModifiedDateTimeUtcNew != changeAdapterItem.ModifiedTimeUtc)
                    {
                        logicalChild.UpdateInfo.ModifiedDateTimeUtcOld = changeAdapterItem.ModifiedTimeUtc;
                    }

                    if (string.CompareOrdinal(logicalChild.UpdateInfo.RelativePath, logicalChild.UpdateInfo.PathOld) != 0)
                    {
                        logicalChild.UpdateInfo.PathOld = logicalChild.UpdateInfo.RelativePath;
                    }

                    // Raise change notification so that the UI can be updated in "real time" rather than waiting for 
                    // the analyze process to finish.
                    this.RaiseChangeDetected(this.sourceAdapter.Configuration.Id, logicalChild.UpdateInfo);

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
                SyncEntry logicalChild = this.sourceAdapter.CreateSyncEntryForAdapterItem(changeAdapterItem, parentEntry);

                knownSyncEntries.Add(changeAdapterItem.UniqueId, logicalChild);

                // Set the NotSynchronized flag so that we know this has not yet been committed to the database.
                logicalChild.State = SyncEntryState.NotSynchronized;

                // Create the update info for the new entry
                logicalChild.UpdateInfo = new EntryUpdateInfo(
                    logicalChild,
                    this.sourceAdapter,
                    GetFlagsForNewSyncEntry(changeAdapterItem),
                    logicalChild.GetRelativePath(db, "/"));

                logicalChild.UpdateInfo.CreationDateTimeUtcNew = changeAdapterItem.CreationTimeUtc;
                logicalChild.UpdateInfo.ModifiedDateTimeUtcNew = changeAdapterItem.ModifiedTimeUtc;
                logicalChild.UpdateInfo.PathNew = logicalChild.UpdateInfo.RelativePath;

                // Some providers can return information that normally isn't known until the item 
                // is copied. Copy the values to the UpdateInfo object.
                if (this.relationship.EncryptionMode == EncryptionMode.Decrypt)
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
                this.RaiseChangeDetected(this.sourceAdapter.Configuration.Id, logicalChild.UpdateInfo);

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

                if (logicalChild.UpdateInfo.HasSyncEntryFlag(SyncEntryChangedFlags.DestinationExists))
                {
                    message += " and the item was also found at the destination";
                }
            }
            else if (logicalChild.UpdateInfo.HasSyncEntryFlag(SyncEntryChangedFlags.IsUpdated))
            {
                message = "The item was updated";
            }
            else if (logicalChild.UpdateInfo.HasSyncEntryFlag(SyncEntryChangedFlags.Renamed))
            {
                message = "The item was renamed";
            }
            else if (logicalChild.UpdateInfo.HasSyncEntryFlag(SyncEntryChangedFlags.Moved))
            {
                message = "The item was moved";
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
                        { "SyncAnalysisRunId", this.analyzeRelationshipResult.Id },
                        { "AdapterId", sourceAdapter.Configuration.Id },
                        { "Id", logicalChild.Id },
                        { "Name", logicalChild.Name },
                        { "Flags", logicalChild.UpdateInfo.GetSetFlagNames() },
                    }));
        }

        /// <summary>
        /// Analyze the changes in files/folders between two adapters without change tracking. For 
        /// non-bi-directional syncing, analyze changes that originate on the source adapter that 
        /// are not present on the destination adapter.
        /// </summary>
        /// <param name="sourceItem">The item read from the source adapter to analyze</param>
        /// <param name="destItem">The item read from the destination adapter to analyze</param>
        /// <param name="logicalParent">The logical item built from database that represents the item being analyzed</param>
        /// <param name="relativePath">The relative path to the item in the folder structure</param>
        /// <remarks>
        /// This method uses a recursive strategy for comparing items exposed by an adapter. This requires retrieving the metadata
        /// for each item on the adapter (expensive for non-local adapters). 
        /// </remarks>
        private void AnalyzeChangesWithoutChangeTracking(
            IAdapterItem sourceItem,
            IAdapterItem destItem,
            SyncEntry logicalParent,
            string relativePath)
        {
            IList<IAdapterItem> sourceChildItems = new List<IAdapterItem>();
            IList<IAdapterItem> destChildItems = new List<IAdapterItem>();

            Logger.Debug(
                Logger.BuildEventMessageWithProperties(
                    "AnalyzeChangesWithoutChangeTracking called with following properties:",
                    new Dictionary<string, object>()
                    {
                        { "SyncAnalysisRunId", this.analyzeRelationshipResult.Id },
                        { "AdapterId", sourceAdapter.Configuration.Id },
                        { "RootName", logicalParent.Name },
                        { "RootId", logicalParent.Id },
                    }));

            // Get files and folders in the given directory. If this given directory is null (such as when a directory was deleted),
            // the list will be empty (allowing deletes to occur recursivly).
            if (sourceItem != null)
            {
                Logger.Debug("Getting child items for source adapter item {0}", sourceItem.FullName);
                sourceChildItems = sourceAdapter.GetAdapterItems(sourceItem).ToList();
            }

            // Get the files and folders in the destination directory
            if (!this.firstSyncComplete && destItem != null)
            {
                Logger.Debug("Getting child items from destination adapter item {0}", destItem.FullName);
                destChildItems = destAdapter.GetAdapterItems(destItem).ToList();
            }

            Logger.Debug("Found {0} child items from adapter.", sourceChildItems.Count);

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
            foreach (IAdapterItem sourceAdapterChild in sourceChildItems)
            {
                if (this.cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                // TODO: Query the adapter somehow to see if the file should be suppressed (sparse file, for example).
                // TODO: Further note - this would be the right place to implement an inclusion/exclusion behavior.

                // Check if there was an error reading the item from the adapter. If so, skip the item.
                if (!string.IsNullOrEmpty(sourceAdapterChild.ErrorMessage))
                {
                    Logger.Info("Skipping adapter child '{0}' with error.", sourceAdapterChild.FullName);
                    continue;
                }

                IAdapterItem destAdapterChild = null;
                if (destChildItems != null)
                {
                    destAdapterChild = destChildItems.FirstOrDefault(i => i.Name == sourceAdapterChild.Name);
                }

                if (destAdapterChild != null)
                {
                    Logger.Debug("Found matching destination adapter child");
                }

                // Get the list of sync entries from the index. We will match these up with the adapter items to determine what has been 
                // added, modified, or removed. Matching is done using the item's adapter ID, which is static for the lifetime of a file
                // and is used to catch situations like a file more or rename.
                Logger.Debug("Examining adapter child item {0}", sourceAdapterChild.FullName);

                // First check if there is an entry that matches the unique ID of the item
                SyncEntry sourceLogicalChild = logicalChildren?.FirstOrDefault(c => c.HasUniqueId(sourceAdapterChild.UniqueId));

                // A match was found, so determine what changes on the entry
                if (sourceLogicalChild != null)
                {
                    Logger.Debug("Found child item {0} in database that matches adapter item.", sourceLogicalChild.Id);

                    // The database already contains an entry for this item. Remove it from the list, then update as needed.
                    logicalChildren.Remove(sourceLogicalChild);

                    if (sourceLogicalChild.State.HasFlag(SyncEntryState.IsDeleted))
                    {
                        Logger.Debug("Child item {0} was un-deleted.", sourceLogicalChild.Id);

                        // Clear the IsDeleted flag (needed in cases where the file was previously deleted).
                        sourceLogicalChild.State &= ~SyncEntryState.IsDeleted;

                        // Create the update info for the new entry (marked as a new file/directory)
                        sourceLogicalChild.UpdateInfo = new EntryUpdateInfo(
                            sourceLogicalChild,
                            sourceAdapter,
                            SyncEntryChangedFlags.Restored,
                            Path.Combine(relativePath, sourceLogicalChild.Name));

                        // Because the item was previously deleted, all of the previous values will be null/empty
                        // Other metadata will need to be set for new properties
                        sourceLogicalChild.UpdateInfo.CreationDateTimeUtcNew = sourceAdapterChild.CreationTimeUtc;
                        sourceLogicalChild.UpdateInfo.ModifiedDateTimeUtcNew = sourceAdapterChild.ModifiedTimeUtc;

                        // Raise change notification so that the UI can be updated in "real time" rather than waiting for the analyze 
                        // process to finish.
                        this.RaiseChangeDetected(sourceAdapter.Configuration.Id, sourceLogicalChild.UpdateInfo);

                        this.LogSyncAnalyzerChangeFound(sourceLogicalChild);
                        continue;
                    }

                    EntryUpdateResult updateResult;

                    // If the item differs from the entry in the index, an update will be required.
                    if (sourceLogicalChild.UpdateInfo == null && 
                        sourceAdapter.IsEntryUpdated(sourceLogicalChild, sourceAdapterChild, out updateResult))
                    {
                        Logger.Debug("Child item {0} is out of sync.", sourceLogicalChild.Id);

                        // Create the update info for the new entry
                        sourceLogicalChild.UpdateInfo = new EntryUpdateInfo(
                            sourceLogicalChild, 
                            sourceAdapter,
                            updateResult.ChangeFlags, 
                            Path.Combine(relativePath, sourceLogicalChild.Name));

                        // Set all of the previous metadata values to those from the sync entry
                        sourceLogicalChild.UpdateInfo.SetOldMetadataFromSyncEntry();

                        // If the source item's creation time differs from the destination item's, set the new
                        // value in the update information.
                        if (sourceLogicalChild.UpdateInfo.CreationDateTimeUtcOld != sourceAdapterChild.CreationTimeUtc)
                        {
                            sourceLogicalChild.UpdateInfo.CreationDateTimeUtcNew = sourceAdapterChild.CreationTimeUtc;
                        }

                        // If the source item's modified time differs from the destination item's, set the new
                        // value in the update information.
                        if (sourceLogicalChild.UpdateInfo.ModifiedDateTimeUtcOld != sourceAdapterChild.ModifiedTimeUtc)
                        {
                            sourceLogicalChild.UpdateInfo.ModifiedDateTimeUtcNew = sourceAdapterChild.ModifiedTimeUtc;
                        }

                        // If the source item's relative path differs from the destination item's, set the new
                        // value in the update information.
                        string newRelativePath = PathUtility.TrimStart(sourceAdapterChild.FullName, 1);
                        if (string.CompareOrdinal(sourceLogicalChild.UpdateInfo.PathOld, newRelativePath) != 0)
                        {
                            sourceLogicalChild.UpdateInfo.PathNew = newRelativePath;
                        }

                        if (this.relationship.EncryptionMode == EncryptionMode.Decrypt)
                        {
                            if (sourceLogicalChild.UpdateInfo.EncryptedSizeOld != sourceAdapterChild.Size)
                            {
                                sourceLogicalChild.UpdateInfo.EncryptedSizeNew = sourceAdapterChild.Size;
                            }
                        }
                        else
                        {
                            if (sourceLogicalChild.UpdateInfo.OriginalSizeOld != sourceAdapterChild.Size)
                            {
                                sourceLogicalChild.UpdateInfo.OriginalSizeNew = sourceAdapterChild.Size;
                            }
                        }

                        // Set the NotSynchronized flag so that we know this has not yet been committed to the database.
                        sourceLogicalChild.State = SyncEntryState.NotSynchronized;

                        // Raise change notification so that the UI can be updated in "real time" rather than waiting for the analyze process to finish.
                        this.RaiseChangeDetected(sourceAdapter.Configuration.Id, sourceLogicalChild.UpdateInfo);
                        this.LogSyncAnalyzerChangeFound(sourceLogicalChild);
                    }
                }
                else
                {
                    Logger.Debug("Child item was not found in database that matches adapter item.");

                    if (movedEntries.Contains(sourceAdapterChild.UniqueId))
                    {
                        Logger.Debug("Child item already processed as a move.");
                        continue;
                    }

                    EntryUpdateInfo updateInfo;
                    if (this.deletedEntries.TryGetValue(sourceAdapterChild.UniqueId, out updateInfo))
                    {
                        Logger.Debug("Child item was previously detected as a delete but was moved.");

                        // This item was detected as having been deleted from a different folder, but was found
                        // in this folder. Remove it from the list of deleted items, and let normal processing 
                        // continue to evaluate the item.
                        this.deletedEntries.Remove(sourceAdapterChild.UniqueId);
                    }

                    SyncEntryAdapterData existingAdapterEntry = null;

                    // Before assuming that the item is new, check if it was moved from a previous location. To do this, first 
                    // check if the item's adapterItemId is already known in the database.
                    if (adapterEntryHashList.Any(h => h == sourceAdapterChild.UniqueId.GetHashCode()))
                    {
                        // The item is probably already in the database (but not for sure, since this was only a hash check
                        // for performance reasons. Find the actual item in the database to be sure.
                        existingAdapterEntry = db.AdapterEntries
                            .Include(e => e.SyncEntry)
                            .FirstOrDefault(e => e.AdapterEntryId == sourceAdapterChild.UniqueId);
                    }

                    if (existingAdapterEntry != null)
                    {
                        sourceLogicalChild = existingAdapterEntry.SyncEntry;

                        // Create the update info for the new entry
                        sourceLogicalChild.UpdateInfo = new EntryUpdateInfo(
                            sourceLogicalChild,
                            sourceAdapter,
                            SyncEntryChangedFlags.Moved,
                            Path.Combine(relativePath, sourceLogicalChild.Name));

                        sourceLogicalChild.UpdateInfo.ParentIdNew = logicalParent.Id;

                        movedEntries.Add(sourceAdapterChild.UniqueId);
                    }
                    else
                    {
                        // This file/directory was not found in the index, so create a new entry for it. Note that while this is a call
                        // to the adapter, no object is created as a result of the call (the result is in-memory only).
                        sourceLogicalChild =
                            sourceAdapter.CreateSyncEntryForAdapterItem(sourceAdapterChild, logicalParent);

                        // Create the update info for the new entry
                        sourceLogicalChild.UpdateInfo = new EntryUpdateInfo(
                            sourceLogicalChild,
                            sourceAdapter,
                            GetFlagsForNewSyncEntry(sourceAdapterChild),
                            Path.Combine(relativePath, sourceLogicalChild.Name));
                    }

                    // Set the NotSynchronized flag so that we know this has not yet been committed to the database.
                    sourceLogicalChild.State = SyncEntryState.NotSynchronized;

                    sourceLogicalChild.UpdateInfo.CreationDateTimeUtcNew = sourceAdapterChild.CreationTimeUtc;
                    sourceLogicalChild.UpdateInfo.ModifiedDateTimeUtcNew = sourceAdapterChild.ModifiedTimeUtc;
                    sourceLogicalChild.UpdateInfo.PathNew = sourceLogicalChild.UpdateInfo.RelativePath;

                    // An optimization can be made when syncing a relationship for the first time. There is a possibility that the
                    // file to be synced already exists in the destination. Check if it does, and if the metadata and at least one
                    // of the hashes matches between the source and the destination. If so, then the actual copy of the file can
                    // be skipped, and only the SyncEntry created.
                    if (destAdapterChild != null)
                    {
                        this.CheckIfSyncRequired(
                            sourceLogicalChild,
                            sourceAdapterChild,
                            destAdapterChild);
                    }

                    // Raise change notification so that the UI can be updated in "real time" rather than waiting for the analyze process to finish.
                    this.RaiseChangeDetected(sourceAdapter.Configuration.Id, sourceLogicalChild.UpdateInfo);
                    this.LogSyncAnalyzerChangeFound(sourceLogicalChild);
                }

                // If this is a directory, descend into it
                if (sourceAdapterChild.ItemType == SyncAdapterItemType.Directory)
                {
                    this.AnalyzeChangesWithoutChangeTracking(
                        sourceAdapterChild,
                        destAdapterChild,
                        sourceLogicalChild,
                        Path.Combine(relativePath, sourceLogicalChild.Name));
                }
            }

            // Any entries still in the indexChildren list are no longer present in the source adapter and
            // need to be expunged (recursivly).
            if (logicalChildren != null)
            {
                foreach (
                    SyncEntry oldChild in logicalChildren.Where(e => e.State.HasFlag(SyncEntryState.IsDeleted) == false))
                {
                    if (this.cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    SyncEntryAdapterData adapterEntry = 
                        oldChild.AdapterEntries.FirstOrDefault(e => e.AdapterId == sourceAdapter.Configuration.Id);

                    if (movedEntries.Contains(adapterEntry.AdapterEntryId))
                    {
                        Logger.Debug("Child item {0} ({1}) detected as deleted, but was moved.", oldChild.Id, oldChild.Name);
                        continue;
                    }

                    Logger.Debug("Child item {0} ({1}) was deleted.", oldChild.Id, oldChild.Name);

                    // Mark the sync entry as 'deleted' by setting the appropriate bit.
                    oldChild.State |= SyncEntryState.IsDeleted;

                    // Set the state to 'Unsynchronized' (so that we know we have pending changes).
                    oldChild.State |= SyncEntryState.NotSynchronized;

                    // Create the update info for the new entry
                    oldChild.UpdateInfo = new EntryUpdateInfo(
                        oldChild,
                        sourceAdapter,
                        SyncEntryChangedFlags.Deleted,
                        Path.Combine(relativePath, oldChild.Name));

                    // Set all previous metadata values from the values in the sync entry. Since this is a 
                    // deletion, all of the previous metadata values will be populated and the current values
                    // will be null/empty.
                    oldChild.UpdateInfo.SetOldMetadataFromSyncEntry();

                    // The item appears to have been deleted, but it may have in fact been moved into a folder
                    // that we have not yet examined. Put the deleted item into the list below. If we find the 
                    // same item in another folder and in this list, then we will know that it was moved. If we
                    // do not find the item anywhere else, then we know that it was in fact deleted.
                    this.deletedEntries.Add(adapterEntry.AdapterEntryId, oldChild.UpdateInfo);

                    // If the entry we are removing is a directory, we need to also check for subdirectories and files. We can do this by calling 
                    // this method recursivly, and passing null for adapter folder (indicating that is no longer exists according to the 
                    // originating adapter.
                    if (oldChild.Type == SyncEntryType.Directory)
                    {
                        this.AnalyzeChangesWithoutChangeTracking(
                            null,
                            null,
                            oldChild,
                            Path.Combine(relativePath, oldChild.Name));
                    }
                }
            }
        }

        /// <summary>
        /// Check if a file needs to be synced, or if it is already present in the destination.
        /// </summary>
        /// <param name="sourceLogicalChild"></param>
        /// <param name="sourceAdapterChild"></param>
        /// <param name="destAdapterChild"></param>
        private void CheckIfSyncRequired(
            SyncEntry sourceLogicalChild, 
            IAdapterItem sourceAdapterChild, 
            IAdapterItem destAdapterChild)
        {
            // First check if encryption is enabled, since that is immediately going to block the
            // ability to use an existing file.
            if (this.relationship.EncryptionMode != EncryptionMode.None)
            {
                return;
            }

            SyncEntryChangedFlags newChangeFlags = sourceLogicalChild.UpdateInfo.Flags;
            sourceLogicalChild.UpdateInfo.ExistingItemId = destAdapterChild.UniqueId;

            if (sourceLogicalChild.UpdateInfo.HasSyncEntryFlag(SyncEntryChangedFlags.NewDirectory))
            {
                newChangeFlags |= SyncEntryChangedFlags.DestinationExists;

                // The directory exists on the destination. Check if the metadata is incorrect.
                if (!DateTimeEqual(sourceAdapterChild.CreationTimeUtc, destAdapterChild.CreationTimeUtc))
                {
                    newChangeFlags |= SyncEntryChangedFlags.CreatedTimestamp;
                }

                if (!DateTimeEqual(sourceAdapterChild.ModifiedTimeUtc, destAdapterChild.ModifiedTimeUtc))
                {
                    newChangeFlags |= SyncEntryChangedFlags.ModifiedTimestamp;
                }

                sourceLogicalChild.UpdateInfo.SetFlags(newChangeFlags);
                return;
            }

            // Verify that we are only dealing with a single new file update
            Pre.Assert(sourceLogicalChild.UpdateInfo.HasSyncEntryFlag(SyncEntryChangedFlags.NewFile));

            bool isSourceSha1Available =
                sourceAdapterChild.Sha1Hash != null || sourceAdapter.Locality != AdapterLocality.Internet;

            bool isDestSha1Available =
                destAdapterChild.Sha1Hash != null || destAdapter.Locality != AdapterLocality.Internet;

            bool isSourceMd5Available =
                sourceAdapterChild.Md5Hash != null || sourceAdapter.Locality != AdapterLocality.Internet;

            bool isDestMd5Available =
                destAdapterChild.Md5Hash != null || destAdapter.Locality != AdapterLocality.Internet;

            if (!isSourceSha1Available && !isSourceMd5Available)
            {
                // No hashes are available from the source and they can't be easily computed, so we 
                // dont know for sure if the file already exists at the destination. Therefore, we
                // will need to copy the file.
                Logger.Debug("CheckIfSyncRequired: No hashes available");
                return;
            }

            // If the length of the files is different, we will have to copy the file (along with metadata)
            if (sourceAdapterChild.Size != destAdapterChild.Size)
            {
                Logger.Debug(
                    "CheckIfSyncRequired: File size differs (source={0}, dest={1})",
                    sourceAdapterChild.Size,
                    destAdapterChild.Size);

                return;
            }

            if (isSourceSha1Available && isDestSha1Available)
            {
                byte[] sourceSha1 = sourceAdapter.GetItemHash(HashType.SHA1, sourceAdapterChild);
                byte[] destSha1 = destAdapter.GetItemHash(HashType.SHA1, destAdapterChild);

                if (!NativeMethods.ByteArrayEquals(sourceSha1, destSha1))
                {
                    // The SHA1 hashes for the files do not match, so we need to copy the file
                    return;
                }

                // Set the SHA1 hash property on the logical objects
                sourceLogicalChild.SetSha1Hash(this.relationship, SyncEntryPropertyLocation.Source, sourceSha1);
                sourceLogicalChild.SetSha1Hash(this.relationship, SyncEntryPropertyLocation.Destination, sourceSha1);
                sourceLogicalChild.UpdateInfo.OriginalSha1HashNew = sourceSha1;

                newChangeFlags |= SyncEntryChangedFlags.DestinationExists;

                Logger.Debug(
                    "The SHA1 hash ({0}) matches source and destination copies of {1}. File copy will be skipped.",
                    "0x" + BitConverter.ToString(sourceSha1).Replace("-", ""),
                    sourceAdapterChild.FullName);
            }
            else if (isSourceMd5Available && isDestMd5Available)
            {
                byte[] sourceMd5 = sourceAdapter.GetItemHash(HashType.MD5, sourceAdapterChild);
                byte[] destMd5 = destAdapter.GetItemHash(HashType.MD5, destAdapterChild);

                if (!NativeMethods.ByteArrayEquals(sourceMd5, destMd5))
                {
                    // The MD5 hashes for the files do not match, so we need to copy the file
                    return;
                }

                // Set the SHA1 hash property on the logical objects
                sourceLogicalChild.SetMd5Hash(this.relationship, SyncEntryPropertyLocation.Source, sourceMd5);
                sourceLogicalChild.SetMd5Hash(this.relationship, SyncEntryPropertyLocation.Destination, sourceMd5);
                sourceLogicalChild.UpdateInfo.OriginalMd5HashNew = sourceMd5;

                newChangeFlags |= SyncEntryChangedFlags.DestinationExists;

                Logger.Debug(
                    "The MD5 hash ({0}) matches source and destination copies of {1}. File copy will be skipped.",
                    "0x" + BitConverter.ToString(sourceMd5).Replace("-", ""),
                    sourceAdapterChild.FullName);
            }
            else
            {
                // Some combination of hashes were not available
                Logger.Debug("Cannot perform hash comparison of {0}", sourceAdapterChild.FullName);
            }

            // We found a hash match for the file (either SHA1 or MD5). Check if the metadata is incorrect.
            if (!DateTimeEqual(sourceAdapterChild.CreationTimeUtc, destAdapterChild.CreationTimeUtc))
            {
                newChangeFlags |= SyncEntryChangedFlags.CreatedTimestamp;
            }

            if (!DateTimeEqual(sourceAdapterChild.ModifiedTimeUtc, destAdapterChild.ModifiedTimeUtc))
            {
                newChangeFlags |= SyncEntryChangedFlags.ModifiedTimestamp;
            }

            sourceLogicalChild.UpdateInfo.SetFlags(newChangeFlags);
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
            this.AnalyzeResult.EntryResults.Add(updateInfo);
            this.ChangeDetected?.Invoke(
                this, 
                new AnalyzeJobProgressInfo(
                    updateInfo, 
                    adapterId,
                    this.AnalyzeResult.EntryResults.Count,
                    0));
        }

        private static bool DateTimeEqual(DateTime dt1, DateTime dt2)
        {
            const long Epsilon = 100;

            return dt1.ToUniversalTime().Ticks - dt2.ToUniversalTime().Ticks < Epsilon;
        }
    }
}