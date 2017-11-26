namespace SyncPro.Data
{
    using System;

    public interface ISyncEntryMetadataChange
    {
        /// <summary>
        /// The previous size in bytes of the entry (if changed)
        /// </summary>
        long SizeOld { get; set; }

        /// <summary>
        /// The size of the entry (in bytes) at the time when it was synced.
        /// </summary>
        long SizeNew { get; set; }

        /// <summary>
        /// The previous SHA1 Hash of the file content (if changed)
        /// </summary>
        byte[] Sha1HashOld { get; set; }

        /// <summary>
        /// The SHA1 Hash of the file content at the time when it was synced.
        /// </summary>
        byte[] Sha1HashNew { get; set; }

        /// <summary>
        /// The previous MD5 Hash of the file content (if changed)
        /// </summary>
        byte[] Md5HashOld { get; set; }

        /// <summary>
        /// The MD5 Hash of the file content at the time when it was synced.
        /// </summary>
        byte[] Md5HashNew { get; set; }

        /// <summary>
        /// The previous CreationTime of the entry (if changed)
        /// </summary>
        DateTime? CreationDateTimeUtcOld { get; set; }

        /// <summary>
        /// The CreationTime of the entry at the time it was synced.
        /// </summary>
        DateTime? CreationDateTimeUtcNew { get; set; }

        /// <summary>
        /// The previous ModifiedTime of the entry (if changed)
        /// </summary>
        DateTime? ModifiedDateTimeUtcOld { get; set; }

        /// <summary>
        /// The ModifiedTime of the entry at the time it was synced.
        /// </summary>
        DateTime? ModifiedDateTimeUtcNew { get; set; }

        /// <summary>
        /// The previous full path of the item (from the root of the adapter) if changed.
        /// </summary>
        string PathOld { get; set; }

        /// <summary>
        /// The full path of the item (from the root of the adapter) when it was synced.
        /// </summary>
        string PathNew { get; set; }
    }
}