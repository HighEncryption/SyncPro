namespace SyncPro.Data
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Diagnostics;
    using System.Linq;

    using SyncPro.Configuration;
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

        /// <summary>
        /// Indicates that an error occurred when reading this entry from the source adapter.
        /// </summary>
        Exception = 0x04,

        Undefined = 0xFF
    }

    public enum SyncEntryPropertyLocation
    {
        Source,
        Destination
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
        /// The SHA1 hash of the unencrypted source file contents
        /// </summary>
        [MaxLength(20)]
        public byte[] OriginalSha1Hash { get; set; }

        /// <summary>
        /// The SHA1 hash of the encrytped file contents. Only set when the file is encrypted.
        /// </summary>
        [MaxLength(20)]
        public byte[] EncryptedSha1Hash { get; set; }

        /// <summary>
        /// The MD5 hash of the unencrypted file contents
        /// </summary>
        [MaxLength(16)]
        public byte[] OriginalMd5Hash { get; set; }

        /// <summary>
        /// The MD5 hash of the encrypted file contents. Only set when the file is encrypted.
        /// </summary>
        [MaxLength(16)]
        public byte[] EncryptedMd5Hash { get; set; }

        /// <summary>
        /// The size of the unencrypted file (bytes)
        /// </summary>
        public long OriginalSize { get; set; }

        /// <summary>
        /// The size of the encrypted file (bytes)
        /// </summary>
        public long EncryptedSize { get; set; }

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

        public string GetRelativePath(SyncDatabase database, string pathSeparator)
        {
            return PathUtility.Join(pathSeparator, GetRelativePathStack(database));
        }

        public IList<string> GetRelativePathStack(SyncDatabase database)
        {
            return this.relativePathStack ?? (this.relativePathStack = GetRelativePathStackInternal(database));
        }

        public long GetSize(SyncRelationship relationship, SyncEntryPropertyLocation location)
        {
            // When decrypting, the source will be encrypted
            if (location == SyncEntryPropertyLocation.Source &&
                relationship.EncryptionMode == EncryptionMode.Decrypt)
            {
                return this.EncryptedSize;
            }

            // When encrypting, the destination will be encrypted
            if (location == SyncEntryPropertyLocation.Destination &&
                relationship.EncryptionMode == EncryptionMode.Encrypt)
            {
                return this.EncryptedSize;
            }

            // For all other cases, use the original (unencrypted) value
            return this.OriginalSize;
        }

        public void SetSize(SyncRelationship relationship, SyncEntryPropertyLocation location, long value)
        {
            // When decrypting, the source will be encrypted
            if (location == SyncEntryPropertyLocation.Source &&
                relationship.EncryptionMode == EncryptionMode.Decrypt)
            {
                this.EncryptedSize = value;
                return;
            }

            // When encrypting, the destination will be encrypted
            if (location == SyncEntryPropertyLocation.Destination &&
                relationship.EncryptionMode == EncryptionMode.Encrypt)
            {
                this.EncryptedSize = value;
                return;
            }

            // For all other cases, use the original (unencrypted) value
            this.OriginalSize = value;
        }

        public byte[] GetSha1Hash(SyncRelationship relationship, SyncEntryPropertyLocation location)
        {
            // When decrypting, the source will be encrypted
            if (location == SyncEntryPropertyLocation.Source &&
                relationship.EncryptionMode == EncryptionMode.Decrypt)
            {
                return this.EncryptedSha1Hash;
            }

            // When encrypting, the destination will be encrypted
            if (location == SyncEntryPropertyLocation.Destination &&
                relationship.EncryptionMode == EncryptionMode.Encrypt)
            {
                return this.EncryptedSha1Hash;
            }

            // For all other cases, use the original (unencrypted) value
            return this.OriginalSha1Hash;
        }

        public void SetSha1Hash(SyncRelationship relationship, SyncEntryPropertyLocation location, byte[] value)
        {
            // When decrypting, the source will be encrypted
            if (location == SyncEntryPropertyLocation.Source &&
                relationship.EncryptionMode == EncryptionMode.Decrypt)
            {
                this.EncryptedSha1Hash = value;
                return;
            }

            // When encrypting, the destination will be encrypted
            if (location == SyncEntryPropertyLocation.Destination &&
                relationship.EncryptionMode == EncryptionMode.Encrypt)
            {
                this.EncryptedSha1Hash = value;
                return;
            }

            // For all other cases, use the original (unencrypted) value
            this.OriginalSha1Hash = value;
        }

        public byte[] GetMd5Hash(SyncRelationship relationship, SyncEntryPropertyLocation location)
        {
            // When decrypting, the source will be encrypted
            if (location == SyncEntryPropertyLocation.Source &&
                relationship.EncryptionMode == EncryptionMode.Decrypt)
            {
                return this.EncryptedMd5Hash;
            }

            // When encrypting, the destination will be encrypted
            if (location == SyncEntryPropertyLocation.Destination &&
                relationship.EncryptionMode == EncryptionMode.Encrypt)
            {
                return this.EncryptedSha1Hash;
            }

            // For all other cases, use the original (unencrypted) value
            return this.OriginalSha1Hash;
        }

        public void SetMd5Hash(SyncRelationship relationship, SyncEntryPropertyLocation location, byte[] value)
        {
            // When decrypting, the source will be encrypted
            if (location == SyncEntryPropertyLocation.Source &&
                relationship.EncryptionMode == EncryptionMode.Decrypt)
            {
                this.EncryptedMd5Hash = value;
                return;
            }

            // When encrypting, the destination will be encrypted
            if (location == SyncEntryPropertyLocation.Destination &&
                relationship.EncryptionMode == EncryptionMode.Encrypt)
            {
                this.EncryptedMd5Hash = value;
                return;
            }

            // For all other cases, use the original (unencrypted) value
            this.OriginalMd5Hash = value;
        }
    }
}