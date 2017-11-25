namespace SyncPro.Data
{
    using System;

    public interface ISyncEntryMetadataChange
    {
        /// <summary>
        /// The previous size in bytes of the entry (if changed)
        /// </summary>
        long SizePrevious { get; set; }

        /// <summary>
        /// The size of the entry (in bytes) at the time when it was synced.
        /// </summary>
        long SizeCurrent { get; set; }

        /// <summary>
        /// The previous SHA1 Hash of the file content (if changed)
        /// </summary>
        byte[] Sha1HashPrevious { get; set; }

        /// <summary>
        /// The SHA1 Hash of the file content at the time when it was synced.
        /// </summary>
        byte[] Sha1HashCurrent { get; set; }

        /// <summary>
        /// The previous MD5 Hash of the file content (if changed)
        /// </summary>
        byte[] Md5HashPrevious { get; set; }

        /// <summary>
        /// The MD5 Hash of the file content at the time when it was synced.
        /// </summary>
        byte[] Md5HashCurrent { get; set; }

        /// <summary>
        /// The previous CreationTime of the entry (if changed)
        /// </summary>
        DateTime CreationDateTimeUtcPrevious { get; set; }

        /// <summary>
        /// The CreationTime of the entry at the time it was synced.
        /// </summary>
        DateTime CreationDateTimeUtcCurrent { get; set; }

        /// <summary>
        /// The previous ModifiedTime of the entry (if changed)
        /// </summary>
        DateTime ModifiedDateTimeUtcPrevious { get; set; }

        /// <summary>
        /// The ModifiedTime of the entry at the time it was synced.
        /// </summary>
        DateTime ModifiedDateTimeUtcCurrent { get; set; }

        /// <summary>
        /// The previous full path of the item (from the root of the adapter) if changed.
        /// </summary>
        string PathPrevious { get; set; }

        /// <summary>
        /// The full path of the item (from the root of the adapter) when it was synced.
        /// </summary>
        string PathCurrent { get; set; }
    }
}