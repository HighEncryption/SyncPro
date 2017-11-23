namespace SyncPro.Data
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    using SyncPro.Adapters;
    using SyncPro.Runtime;

    /// <summary>
    /// Contains metadata about the changes synchronized for a <see cref="SyncEntry"/>.
    /// </summary>
    [Table("HistoryEntries")]
    public class SyncHistoryEntryData
    {
        [Key]
        public int Id { get; set; }

        public int SyncHistoryId { get; set; }

        public long SyncEntryId { get; set; }

        [ForeignKey("SyncEntryId")]
        public virtual SyncEntry SyncEntry { get; set; }

        /// <summary>
        /// The size of the entry when it was synced.
        /// </summary>
        public long Size { get; set; }

        public byte[] Sha1Hash { get; set; }

        /// <summary>
        /// The result of the change (succeeded/failed).
        /// </summary>
        public EntryUpdateState Result { get; set; }

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

        /// <summary>
        /// The timestamp when the change was applied (and this <see cref="SyncHistoryEntryData"/> was created).
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// The original full path of the item (from the root of the adapter) when it was synced.
        /// </summary>
        public string OriginalPath { get; set; }
    }
}