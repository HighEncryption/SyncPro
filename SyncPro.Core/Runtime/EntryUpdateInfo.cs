namespace SyncPro.Runtime
{
    using System.Diagnostics;

    using SyncPro.Adapters;
    using SyncPro.Data;

    public enum EntryUpdateState
    {
        Undefined,
        NotStarted,
        Succeeded,
        Failed
    }

    /// <summary>
    /// Contains information about the changes being made to an entry (file/folder) such as the
    /// type of change (add/delete/update/etc), the state of the change (NoStarted/Failed/Succeeded),
    /// and any errors that resulted.
    /// </summary>
    /// <remarks>
    /// The lifetime of this object typically starts during the analyze stage when a change is first 
    /// detected in an entry. This object is then persisted until it is applied by the entry being
    /// synchronized, or when the analysis result is disposed (and the change is abandoned).
    /// </remarks>
    [DebuggerDisplay("{Entry.Name}")]
    public class EntryUpdateInfo
    {
        /// <summary>
        /// Then entry being changed.
        /// </summary>
        public SyncEntry Entry { get; }

        /// <summary>
        /// The path of the entry relative to the root that the entry is synced from.
        /// </summary>
        public string RelativePath { get; }

        /// <summary>
        /// The adapter that the change originated from.
        /// </summary>
        public AdapterBase OriginatingAdapter { get; }

        /// <summary>
        /// Flags indicating the type of change to be applied (added/updated/deleted/etc).
        /// </summary>
        public SyncEntryChangedFlags Flags { get; }

        /// <summary>
        /// The state of the change (whether it has been applied/succeeded).
        /// </summary>
        public EntryUpdateState State { get; set; }

        /// <summary>
        /// The error message that resulted from applying the change.
        /// </summary>
        public string ErrorMessage { get; set; }

        public bool HasSyncEntryFlag(SyncEntryChangedFlags flag)
        {
            return (this.Flags & flag) != 0;
        }

        public EntryUpdateInfo(SyncEntry entry, AdapterBase originatingAdapter, SyncEntryChangedFlags flags, string relativePath)
        {
            Pre.ThrowIfArgumentNull(entry, "entry");
            Pre.ThrowIfArgumentNull(originatingAdapter, "originatingAdapter");

            this.Entry = entry;
            this.OriginatingAdapter = originatingAdapter;
            this.Flags = flags;
            this.RelativePath = relativePath;

            if (flags != SyncEntryChangedFlags.None)
            {
                this.State = EntryUpdateState.NotStarted;
            }
        }
    }
}