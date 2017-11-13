namespace SyncPro.Data
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Diagnostics;
    using System.Linq;

    using SyncPro.Runtime;
    using SyncPro.Utility;

    public enum SyncEntryType : short
    {
        Undefined = 0,
        Directory = 1,
        File = 2,
    }

    [Flags]
    public enum SyncEntryState : short
    {
        None = 0x00,

        /// <summary>
        /// Indicates that the entry has not yet been committed to the database. This is used when the entry is created in-memory (specifically
        /// during the analyze phase).
        /// </summary>
        NotSynchronized = 0x01,

        /// <summary>
        /// The entry has been deleted from the adapter, but the entry remains in the database
        /// </summary>
        IsDeleted = 0x02,
        Undefined = 0xFF
    }

    /// <summary>
    /// Represents an entry (typically a file or folder) that is tracked in the database.
    /// </summary>
    [DebuggerDisplay("{Name} ({Id})")]
    [Table("Entries")]
    public class SyncEntry
    {
        /// <summary>
        /// The unique ID for this entry
        /// </summary>
        [Key]
        public long Id { get; set; }

        public long? ParentId { get; set; }

        [NotMapped]
        public SyncEntry ParentEntry { get; set; }

        /// <summary>
        /// The type of the entry (file or directory)
        /// </summary>
        public SyncEntryType Type { get; set; }

        /// <summary>
        /// The name of the entry 
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The UTC time of when the entry was created
        /// </summary>
        public DateTime CreationDateTimeUtc { get; set; }

        /// <summary>
        /// The UTC time of when the entry was last modified
        /// </summary>
        public DateTime ModifiedDateTimeUtc { get; set; }

        /// <summary>
        /// The UTC time of when the entry was last synchronized
        /// </summary>
        public DateTime EntryLastUpdatedDateTimeUtc { get; set; }

        /// <summary>
        /// The SHA1 hash of the file contents
        /// </summary>
        [MaxLength(20)]
        public byte[] Sha1Hash { get; set; }

        /// <summary>
        /// The MD5 hash of the file contents
        /// </summary>
        [MaxLength(16)]
        public byte[] Md5Hash { get; set; }

        /// <summary>
        /// The size of the file (bytes)
        /// </summary>
        public long Size { get; set; }

        public SyncEntryState State { get; set; }

        public virtual ICollection<SyncEntryAdapterData> AdapterEntries { get; set; }

        [NotMapped]
        public EntryUpdateInfo UpdateInfo { get; set; }

        [NotMapped]
        internal SyncDatabase OriginatingDatabase { get; set; }

        public bool HasUniqueId(string id)
        {
            return this.AdapterEntries != null && this.AdapterEntries.Any(e => e.AdapterEntryId == id);
        }

        private IList<string> relativePathStack;

        private IList<string> GetRelativePathStackInternal(SyncDatabase database)
        {
            if (database == null)
            {
                Debugger.Break();
                return null;
            }

            SyncEntry entry = this;
            List<string> resultList = new List<string>();

            while (entry != null)
            {
                if (entry.ParentEntry != null)
                {
                    resultList.Add(entry.Name);
                    entry = entry.ParentEntry;
                }
                else if (entry.ParentId != null)
                {
                    resultList.Add(entry.Name);
                    entry = database.Entries.FirstOrDefault(e => e.Id == entry.ParentId.Value);
                }
                else
                {
                    break;
                }
            }

            resultList.Reverse();
            return resultList;
        }

        //private string relativePath;

        internal string GetRelativePath(SyncDatabase database, string pathSeparator)
        {
            return PathUtility.Join(pathSeparator, this.GetRelativePathStack(database));
        }

        internal IList<string> GetRelativePathStack(SyncDatabase database)
        {
            return this.relativePathStack ?? (this.relativePathStack = this.GetRelativePathStackInternal(database));
        }
    }

    /// <summary>
    /// Contains the adapter-specific information about a <see cref="SyncEntry"/>.
    /// </summary>
    /// <remarks>
    /// The <see cref="SyncEntry"/> object contains information about a file or folder that is synchronized via
    /// a <see cref="SyncRelationship"/>. However, each adapter needs to maintain adapter-specifc information 
    /// about the <see cref="SyncEntry"/>, such as it's own internal name of the entry. This class provides the
    /// "glue" between the <see cref="SyncEntry"/> and how each adapter identifies that <see cref="SyncEntry"/>.
    /// </remarks>
    [Table("SyncEntryAdapterData")]
    public class SyncEntryAdapterData
    {
        /// <summary>
        /// The unique identifier for the <see cref="SyncEntryAdapterData"/>. This value is unique within a sync 
        /// relationship.
        /// </summary>
        [Key]
        public long Id { get; set; }

        /// <summary>
        /// The ID of the <see cref="SyncEntry"/> that this <see cref="SyncEntryAdapterData"/> is related to.
        /// </summary>
        public long SyncEntryId { get; set; }

        /// <summary>
        /// The <see cref="SyncEntry"/> that this <see cref="SyncEntryAdapterData"/> is related to.
        /// </summary>
        [ForeignKey("SyncEntryId")]
        public virtual SyncEntry SyncEntry { get; set; }

        /// <summary>
        /// The numeric identifier for this adapter.
        /// </summary>
        /// <remarks>
        /// Adapter IDs typically start at 1 and increment. Each adapter will have a unique ID within a relationship
        /// and the correspond to the ID stored in the adapter's configuration.
        /// </remarks>
        public int AdapterId { get; set; }

        /// <summary>
        /// The string that the adapter uses to uniquely idenfity this <see cref="SyncEntry"/>.
        /// </summary>
        [MaxLength(128)]
        public string AdapterEntryId { get; set; }

        /// <summary>
        /// Reserved for future use.
        /// </summary>
        public string ExtensionData { get; set; }
    }
}