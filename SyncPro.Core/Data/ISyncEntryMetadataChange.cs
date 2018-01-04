namespace SyncPro.Data
{
    using System;

    public interface ISyncEntryMetadataChange
    {
        /// <summary>
        /// The unencrypted size in bytes of the entry (if changed)
        /// </summary>
        long OriginalSizeOld { get; set; }

        /// <summary>
        /// The unencryptedsize of the entry (in bytes) at the time when it was synced.
        /// </summary>
        long OriginalSizeNew { get; set; }

        /// <summary>
        /// The encrypted size in bytes of the entry (if changed)
        /// </summary>
        long EncryptedSizeOld { get; set; }

        /// <summary>
        /// The encrypted size of the entry (in bytes) at the time when it was synced.
        /// </summary>
        long EncryptedSizeNew { get; set; }

        /// <summary>
        /// The unencrypted SHA1 Hash of the file content (if changed)
        /// </summary>
        byte[] OriginalSha1HashOld { get; set; }

        /// <summary>
        /// The SHA1 Hash of the file content at the time when it was synced.
        /// </summary>
        byte[] OriginalSha1HashNew { get; set; }

        /// <summary>
        /// The previous encrytped SHA1 Hash of the file content (if changed)
        /// </summary>
        byte[] EncryptedSha1HashOld { get; set; }

        /// <summary>
        /// The SHA1 Hash of the encrypted file content at the time when it was synced.
        /// </summary>
        byte[] EncryptedSha1HashNew { get; set; }

        /// <summary>
        /// The previous MD5 Hash of the file content (if changed)
        /// </summary>
        byte[] OriginalMd5HashOld { get; set; }

        /// <summary>
        /// The MD5 Hash of the file content at the time when it was synced.
        /// </summary>
        byte[] OriginalMd5HashNew { get; set; }

        /// <summary>
        /// The previous MD5 Hash of the file content (if changed)
        /// </summary>
        byte[] EncryptedMd5HashOld { get; set; }

        /// <summary>
        /// The MD5 Hash of the file content at the time when it was synced.
        /// </summary>
        byte[] EncryptedMd5HashNew { get; set; }

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