namespace SyncPro.Data
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    using SyncPro.Adapters;
    using SyncPro.Runtime;

    /// <summary>
    /// Contains information about the changes synchronized for a <see cref="SyncEntry"/>.
    /// </summary>
    [Table("HistoryEntries")]
    public class SyncHistoryEntryData : ISyncEntryMetadataChange
    {
        /// <summary>
        /// The database ID of the history entry.
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// 
        /// The ID of the <see cref="SyncHistoryData"/> that this entry was created in.
        /// </summary>
        public int SyncHistoryId { get; set; }

        /// <summary>
        /// The ID of the <see cref="SyncEntry"/> this this entry refers to.
        /// </summary>
        public long SyncEntryId { get; set; }

        /// <summary>
        /// The <see cref="SyncEntry"/> this this entry refers to.
        /// </summary>
        [ForeignKey("SyncEntryId")]
        public virtual SyncEntry SyncEntry { get; set; }

        /// <summary>
        /// The result of the change (succeeded/failed).
        /// </summary>
        public EntryUpdateState Result { get; set; }

        /// <summary>
        /// The timestamp when the change was applied (and this <see cref="SyncHistoryEntryData"/> was created).
        /// </summary>
        public DateTime Timestamp { get; set; }

        #region Metadata Properties

        /// <summary>
        /// The previous size in bytes of the entry (if changed)
        /// </summary>
        public long SizeOld { get; set; }

        /// <summary>
        /// The size of the entry (in bytes) at the time when it was synced.
        /// </summary>
        public long SizeNew { get; set; }

        /// <summary>
        /// The previous SHA1 Hash of the file content (if changed)
        /// </summary>
        public byte[] Sha1HashOld { get; set; }

        /// <summary>
        /// The SHA1 Hash of the file content at the time when it was synced.
        /// </summary>
        public byte[] Sha1HashNew { get; set; }

        /// <summary>
        /// The previous MD5 Hash of the file content (if changed)
        /// </summary>
        public byte[] Md5HashOld { get; set; }

        /// <summary>
        /// The MD5 Hash of the file content at the time when it was synced.
        /// </summary>
        public byte[] Md5HashNew { get; set; }

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

        /// <summary>
        /// Flags indicating the way in which the entry changed (size, hash, timestap, etc.)
        /// </summary>
        [NotMapped]
        public SyncEntryChangedFlags Flags
        {
            get
            {
                unchecked
                {
                    return (SyncEntryChangedFlags)(uint)this.FlagsValue;
                }
            }
            set
            {
                unchecked
                {
                    this.FlagsValue = (int) value;
                }
            }
        }

        public bool HasSyncEntryFlag(SyncEntryChangedFlags flag)
        {
            return (this.Flags & flag) != 0;
        }

        public int FlagsValue { get; set; }
    }
}