namespace SyncPro.Runtime
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Text;

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
    /// modified metadata (timestamps/size/hashes), and any errors that resulted.
    /// </summary>
    /// <remarks>
    /// The lifetime of this object typically starts during the analyze stage when a change is first 
    /// detected in an entry. This object is then persisted until it is applied by the entry being
    /// synchronized, or when the analysis result is disposed (and the change is abandoned).
    /// </remarks>
    [DebuggerDisplay("{Entry.Name}")]
    public class EntryUpdateInfo : ISyncEntryMetadataChange
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

        // The region below is the implementation of the ISyncEntryMetadataChange members
        // for tracking the metadata changes for a sync entry. If there are any missing 
        // members, simply copy this same block from the SyncHistoryEntryData class.
        #region Metadata Properties

        /// <summary>
        /// The previous size in bytes of the entry (if changed)
        /// </summary>
        public long SourceSizeOld { get; set; }

        /// <summary>
        /// The size of the entry (in bytes) at the time when it was synced.
        /// </summary>
        public long SourceSizeNew { get; set; }
        /// <summary>
        /// The previous size in bytes of the entry (if changed)
        /// </summary>
        public long DestinationSizeOld { get; set; }

        /// <summary>
        /// The size of the entry (in bytes) at the time when it was synced.
        /// </summary>
        public long DestinationSizeNew { get; set; }

        /// <summary>
        /// The previous SHA1 Hash of the file content (if changed)
        /// </summary>
        public byte[] SourceSha1HashOld { get; set; }

        /// <summary>
        /// The SHA1 Hash of the file content at the time when it was synced.
        /// </summary>
        public byte[] SourceSha1HashNew { get; set; }

        /// <summary>
        /// The previous SHA1 Hash of the file content (if changed)
        /// </summary>
        public byte[] DestinationSha1HashOld { get; set; }

        /// <summary>
        /// The SHA1 Hash of the file content at the time when it was synced.
        /// </summary>
        public byte[] DestinationSha1HashNew { get; set; }

        /// <summary>
        /// The previous MD5 Hash of the file content (if changed)
        /// </summary>
        public byte[] SourceMd5HashOld { get; set; }

        /// <summary>
        /// The MD5 Hash of the file content at the time when it was synced.
        /// </summary>
        public byte[] SourceMd5HashNew { get; set; }

        /// <summary>
        /// The previous MD5 Hash of the file content (if changed)
        /// </summary>
        public byte[] DestinationMd5HashOld { get; set; }

        /// <summary>
        /// The MD5 Hash of the file content at the time when it was synced.
        /// </summary>
        public byte[] DestinationMd5HashNew { get; set; }

        /// <summary>
        /// The previous CreationTime of the entry (if changed)
        /// </summary>
        public DateTime? CreationDateTimeUtcOld { get; set; }

        /// <summary>
        /// The CreationTime of the entry at the time it was synced.
        /// </summary>
        public DateTime? CreationDateTimeUtcNew { get; set; }

        /// <summary>
        /// The previous ModifiedTime of the entry (if changed)
        /// </summary>
        public DateTime? ModifiedDateTimeUtcOld { get; set; }

        /// <summary>
        /// The ModifiedTime of the entry at the time it was synced.
        /// </summary>
        public DateTime? ModifiedDateTimeUtcNew { get; set; }

        /// <summary>
        /// The previous full path of the item (from the root of the adapter) if changed.
        /// </summary>
        public string PathOld { get; set; }

        /// <summary>
        /// The full path of the item (from the root of the adapter) when it was synced.
        /// </summary>
        public string PathNew { get; set; }

        #endregion

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

        private EntryUpdateInfo(SyncEntryChangedFlags flags, string relativePath)
        {
            this.Flags = flags;
            this.RelativePath = relativePath;
        }

        internal static EntryUpdateInfo CreateForTests(SyncEntryChangedFlags flags, string relativePath)
        {
            return new EntryUpdateInfo(flags, relativePath);
        }

        public void SetOldMetadataFromSyncEntry()
        {
            this.SourceSizeOld = this.Entry.SourceSize;
            this.DestinationSizeOld = this.Entry.DestinationSize;

            this.SourceSha1HashOld = this.Entry.SourceSha1Hash;
            this.DestinationSha1HashOld = this.Entry.DestinationSha1Hash;

            this.SourceMd5HashOld = this.Entry.SourceMd5Hash;
            this.DestinationMd5HashOld = this.Entry.DestinationMd5Hash;

            this.CreationDateTimeUtcOld = this.Entry.CreationDateTimeUtc;
            this.ModifiedDateTimeUtcOld = this.Entry.ModifiedDateTimeUtc;
            this.PathOld = this.Entry.UpdateInfo.RelativePath;
        }

        public void SetNewMetadataFromSyncEntry()
        {
            this.SourceSizeNew = this.Entry.SourceSize;
            this.DestinationSizeNew = this.Entry.DestinationSize;

            this.SourceSha1HashNew = this.Entry.SourceSha1Hash;
            this.DestinationSha1HashNew = this.Entry.DestinationSha1Hash;

            this.SourceMd5HashNew = this.Entry.SourceMd5Hash;
            this.DestinationMd5HashNew = this.Entry.DestinationMd5Hash;

            this.CreationDateTimeUtcNew = this.Entry.CreationDateTimeUtc;
            this.ModifiedDateTimeUtcNew = this.Entry.ModifiedDateTimeUtc;
            this.PathNew = this.Entry.UpdateInfo.RelativePath;
        }

        public SyncHistoryEntryData CreateSyncHistoryEntryData()
        {
            return new SyncHistoryEntryData()
            {
                Result = this.State,
                Flags = this.Flags,
                Timestamp = DateTime.Now,

                // Keep these in order according to SyncHistoryEntryData
                SourceSizeOld = this.SourceSizeOld,
                DestinationSizeOld = this.DestinationSizeOld,
                SourceSizeNew = this.SourceSizeNew,
                DestinationSizeNew = this.DestinationSizeNew,

                SourceSha1HashOld = this.SourceSha1HashOld,
                DestinationSha1HashOld = this.DestinationSha1HashOld,
                SourceSha1HashNew = this.SourceSha1HashNew,
                DestinationSha1HashNew = this.DestinationSha1HashNew,

                SourceMd5HashOld = this.SourceMd5HashOld,
                DestinationMd5HashOld = this.DestinationMd5HashOld,
                SourceMd5HashNew = this.SourceMd5HashNew,
                DestinationMd5HashNew = this.DestinationMd5HashNew,

                CreationDateTimeUtcOld = this.CreationDateTimeUtcOld,
                CreationDateTimeUtcNew = this.CreationDateTimeUtcNew,

                ModifiedDateTimeUtcOld = this.ModifiedDateTimeUtcOld,
                ModifiedDateTimeUtcNew = this.ModifiedDateTimeUtcNew,

                PathOld = this.PathOld,
                PathNew = this.PathNew,
            };
        }

        private static readonly ConcurrentDictionary<uint, string> FlagLookup = 
            new ConcurrentDictionary<uint, string>();

        public string GetSetFlagNames()
        {
            string value;
            if (FlagLookup.TryGetValue((uint) this.Flags, out value))
            {
                return value;
            }

            value = this.BuildFlagString(this.Flags);

            FlagLookup.TryAdd((uint) this.Flags, value);

            return value;
        }

        private string BuildFlagString(SyncEntryChangedFlags flags)
        {
            StringBuilder sb = new StringBuilder();

            if (flags == SyncEntryChangedFlags.None)
            {
                return "None";
            }

            if ((flags & SyncEntryChangedFlags.CreatedTimestamp) != 0)
            {
                sb.Append("CreatedTimestamp,");
            }

            if ((flags & SyncEntryChangedFlags.Md5Hash) != 0)
            {
                sb.Append("Md5Hash,");
            }

            if ((flags & SyncEntryChangedFlags.Sha1Hash) != 0)
            {
                sb.Append("Sha1Hash,");
            }

            if ((flags & SyncEntryChangedFlags.Deleted) != 0)
            {
                sb.Append("Deleted,");
            }

            if ((flags & SyncEntryChangedFlags.FileSize) != 0)
            {
                sb.Append("FileSize,");
            }

            if ((flags & SyncEntryChangedFlags.ModifiedTimestamp) != 0)
            {
                sb.Append("ModifiedTimestamp,");
            }

            if ((flags & SyncEntryChangedFlags.NewDirectory) != 0)
            {
                sb.Append("NewDirectory,");
            }

            if ((flags & SyncEntryChangedFlags.NewFile) != 0)
            {
                sb.Append("NewFile,");
            }

            if ((flags & SyncEntryChangedFlags.Renamed) != 0)
            {
                sb.Append("Renamed,");
            }

            if ((flags & SyncEntryChangedFlags.Restored) != 0)
            {
                sb.Append("Restored,");
            }

            return sb.ToString(0, sb.Length - 1);
        }
    }
}