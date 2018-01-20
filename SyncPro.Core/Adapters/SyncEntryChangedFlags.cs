namespace SyncPro.Adapters
{
    using System;

    /// <summary>
    /// Enumeration for the type of changes that can occur for a sync entry.
    /// </summary>
    [Flags]
    public enum SyncEntryChangedFlags : uint
    {
        /// <summary>
        /// Indicates that no changes were made to the entry
        /// </summary>
        None = 0x0000,

        /// <summary>
        /// Indicates that the change is a new file
        /// </summary>
        NewFile = 0x0001,

        /// <summary>
        /// Indicates that the change is a new directory
        /// </summary>
        NewDirectory = 0x0002,

        /// <summary>
        /// Indicates that the creation timestamp has changed
        /// </summary>
        CreatedTimestamp = 0x0004,

        /// <summary>
        /// Indicates that the last modified timestamp has changed
        /// </summary>
        ModifiedTimestamp = 0x0008,

        /// <summary>
        /// Indicates that the size of the file has changes (files only)
        /// </summary>
        FileSize = 0x0010,

        /// <summary>
        /// Indicates that the SHA1 has of the file has changed (files only)
        /// </summary>
        Sha1Hash = 0x0020,

        /// <summary>
        /// Indicates that the file or directory has been renamed
        /// </summary>
        Renamed = 0x0040,

        /// <summary>
        /// Inidicates that the file or directory has been deleted
        /// </summary>
        Deleted = 0x0080,

        /// <summary>
        /// Indicates that the file or directory was restored after being previously deleted.
        /// </summary>
        Restored = 0x0100,

        /// <summary>
        /// Indicates that the MD5 has of the file has changed (files only)
        /// </summary>
        Md5Hash = 0x0200,

        /// <summary>
        /// Indicates that the contents of the file are already present on the destination.
        /// </summary>
        FileExists = 0x0400,

        /// <summary>
        /// Indicates that a directory is already present on the destination.
        /// </summary>
        DirectoryExists = 0x0800,

        /// <summary>
        /// Indicates that the change is a new file or directory
        /// </summary>
        IsNew = NewFile | NewDirectory,

        // Should Renamed be included here?
        IsUpdated = CreatedTimestamp | ModifiedTimestamp | FileSize | Sha1Hash | Md5Hash,
        IsNewOrUpdated = IsNew | IsUpdated,

        /// <summary>
        /// The change is undefined
        /// </summary>
        Undefined = 0xFFFFFFFF
    }
}