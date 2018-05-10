namespace SyncPro.Runtime
{
    using System;
    using System.Collections.Generic;

    using SyncPro.Adapters;

    public class AnalyzeAdapterResult
    {
        public List<EntryUpdateInfo> EntryResults { get; }

        /// <summary>
        /// Inidicates whether files will be synced from the source to the destination
        /// </summary>
        public bool SyncSourceToDestination { get; set; }

        /// <summary>
        /// Inidicates whether files will be synced from the destination to the source
        /// </summary>
        public bool SyncDestinationToSource { get; set; }

        public TrackedChange TrackedChanges { get; set; }

        /// <summary>
        /// Indicates whether there is a tracked change that needs to be committed even if there are no changes that need
        /// to be synchronized.
        /// </summary>
        /// <remarks>
        /// This property is set to true during the analyze phase when at least one adapter supports change tracking but does
        /// not have any tracked state. If synchronization is performed (even when no changes are synced), the tracked changes
        /// will be committed to the adapter.
        /// </remarks>
        public bool ForceCommitChanges { get; set; }

        /// <summary>
        /// Exception thrown during analysis.
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// The number of unchanged files
        /// </summary>
        public int UnchangedFileCount { get; set; }

        /// <summary>
        /// The number of unchanged files
        /// </summary>
        public long UnchangedFileBytes { get; set; }

        /// <summary>
        /// The number of unchanged folders
        /// </summary>
        public int UnchangedFolderCount { get; set; }


        public AnalyzeAdapterResult()
        {
            this.EntryResults = new List<EntryUpdateInfo>();
        }
    }
}