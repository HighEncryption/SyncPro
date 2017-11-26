namespace SyncPro.Adapters.WindowsFileSystem
{
    using System;
    using System.IO;

    using JsonLog;

    public class FileSystemFolder : AdapterItem
    {
        public FileSystemInfo FileSystemInfo { get; }

        public static FileSystemFolder Create(FileSystemInfo fileSystemInfo, IAdapterItem parent, AdapterBase adapter)
        {
            var itemType = fileSystemInfo.Attributes.HasFlag(FileAttributes.Directory)
                ? SyncAdapterItemType.Directory
                : SyncAdapterItemType.File;

            try
            {
                string uniqueId = GetUniqueIdForFileSystemInfo(fileSystemInfo);
                long size = 0;

                FileInfo fileInfo = fileSystemInfo as FileInfo;
                if (fileInfo != null)
                {
                    size = fileInfo.Length;
                }

                return new FileSystemFolder(
                    fileSystemInfo.Name, 
                    parent, 
                    adapter, 
                    itemType, 
                    uniqueId, 
                    fileSystemInfo, 
                    size,
                    fileSystemInfo.CreationTimeUtc,
                    fileSystemInfo.LastWriteTimeUtc);
            }
            catch (Exception exception)
            {
                Logger.Debug("Failed to generate FileSystemFolder for '{0}'. The error was: {1}", fileSystemInfo.Name,
                    exception.Message);

                return new FileSystemFolder(
                    fileSystemInfo.Name, 
                    parent, 
                    adapter, 
                    itemType, 
                    null, 
                    fileSystemInfo, 
                    0,
                    DateTime.MinValue, 
                    DateTime.MinValue)
                {
                    ErrorMessage = exception.Message
                };
            }
        }

        public static FileSystemFolder Create(DirectoryInfo directoryInfo, AdapterBase adapter, bool isDrive)
        {
            try
            {
                string uniqueId = GetUniqueIdForFileSystemInfo(directoryInfo);

                return new FileSystemFolder(
                    directoryInfo.Name, 
                    null, 
                    adapter, 
                    SyncAdapterItemType.Directory, 
                    uniqueId,
                    directoryInfo,
                    0,
                    directoryInfo.CreationTimeUtc,
                    directoryInfo.LastWriteTimeUtc);
            }
            catch (Exception exception)
            {
                Logger.Debug("Failed to generate FileSystemFolder for '{0}'. The error was: {1}", directoryInfo.Name,
                    exception.Message);

                return new FileSystemFolder(
                    directoryInfo.Name, 
                    null, 
                    adapter, 
                    SyncAdapterItemType.Directory, 
                    null,
                    directoryInfo,
                    0,
                    DateTime.MinValue,
                    DateTime.MinValue)
                {
                    ErrorMessage = exception.Message
                };
            }
        }

        private FileSystemFolder(
            string name,
            IAdapterItem parent,
            AdapterBase adapter,
            SyncAdapterItemType itemType,
            string uniqueId,
            FileSystemInfo fileSystemInfo,
            long size,
            DateTime creationTimeUtc,
            DateTime modifiedTimeUtc)
            : base(
                  name, 
                  parent, 
                  adapter, 
                  itemType, 
                  uniqueId, 
                  size,
                  creationTimeUtc,
                  modifiedTimeUtc)
        {
            this.FileSystemInfo = fileSystemInfo;
        }

        //public FileSystemFolder(DirectoryInfo directoryInfo, IAdapterItem parent, AdapterBase adapter, bool isDrive)
        //    : base(directoryInfo.Name, parent, adapter, SyncAdapterItemType.Directory)
        //{
        //    this.FileSystemInfo = directoryInfo;

        //    //if (directoryInfo.Attributes.HasFlag(FileAttributes.Hidden) && !isDrive)
        //    //{
        //    //    this.IsHidden = true;
        //    //}
        //}

        //public FileSystemFolder(FileSystemInfo fileInfo, IAdapterItem parent, AdapterBase adapter)
        //    : base(
        //        fileInfo.Name,
        //        parent,
        //        adapter,
        //        fileInfo.Attributes.HasFlag(FileAttributes.Directory) ? SyncAdapterItemType.Directory : SyncAdapterItemType.File)
        //{
        //    this.FileSystemInfo = fileInfo;

        //    //if (fileInfo.Attributes.HasFlag(FileAttributes.Hidden))
        //    //{
        //    //    this.IsHidden = true;
        //    //}
        //}

        //public FileSystemFolder(DirectoryInfo directoryInfo, AdapterBase adapter, bool isDrive)
        //    : base(directoryInfo.Name, null, adapter, SyncAdapterItemType.Directory)
        //{
        //    this.FileSystemInfo = directoryInfo;

        //    //if (directoryInfo.Attributes.HasFlag(FileAttributes.Hidden) && !isDrive)
        //    //{
        //    //    this.IsHidden = true;
        //    //}

        //    //this.DisplayName = displayName;
        //}

        public static string GetUniqueIdForFileSystemInfo(FileSystemInfo fileInfo)
        {
            if (fileInfo.Attributes.HasFlag(FileAttributes.Directory))
            {
                return Convert.ToBase64String(NativeMethodHelpers.GetDirectoryObjectId(fileInfo.FullName));
            }

            return Convert.ToBase64String(NativeMethodHelpers.GetFileObjectId(fileInfo.FullName));
        }
    }
}