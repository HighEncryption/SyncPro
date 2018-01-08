namespace SyncPro.Adapters.WindowsFileSystem
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    using SyncPro.Configuration;
    using SyncPro.Data;
    using SyncPro.Runtime;
    using SyncPro.Tracing;

    public class WindowsFileSystemAdapter : AdapterBase
    {
        public static readonly Guid TargetTypeId = new Guid("b1755e86-381e-4e78-b47d-dbbfeee34585");

        public WindowsFileSystemAdapterConfiguration Config =>
            (WindowsFileSystemAdapterConfiguration) this.Configuration;

        public override Guid GetTargetTypeId()
        {
            return TargetTypeId;
        }

        public override async Task<SyncEntry> CreateRootEntry()
        {
            return await Task.Run(() =>
            {
                DirectoryInfo d = new DirectoryInfo(this.Config.RootDirectory);
                var rootEntry = this.CreateEntry(d, null);

                // This is the root entry, so re-write the name.
                rootEntry.Name = "[root]";

                return rootEntry;
            }).ConfigureAwait(false);
        }

        public override async Task<IAdapterItem> GetRootFolder()
        {
            return await Task.Factory.StartNew(() =>
            {
                if (this.Config.RootDirectory == null)
                {
                    throw new InvalidOperationException("The root directory has not been set.");
                }

                DirectoryInfo rootDirectoryInfo = new DirectoryInfo(this.Config.RootDirectory);

                return FileSystemFolder.Create(rootDirectoryInfo, this, false);
            }).ConfigureAwait(false);
        }

        public override async Task CreateItemAsync(SyncEntry entry)
        {
            await Task.Factory.StartNew(() =>
            {
                string fullPath;
                using (var database = this.Relationship.GetDatabase())
                {
                    fullPath = Path.Combine(this.Config.RootDirectory, entry.GetRelativePath(database, this.PathSeparator));
                }

                FileSystemInfo fileSystemInfo;
                switch (entry.Type)
                {
                    case SyncEntryType.Directory:
                        fileSystemInfo = Directory.CreateDirectory(fullPath);
                        break;
                    case SyncEntryType.File:
                        using (File.Create(fullPath))
                        {
                            fileSystemInfo = new FileInfo(fullPath);
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                entry.AdapterEntries.Add(
                    new SyncEntryAdapterData()
                    {
                        SyncEntryId = entry.Id,
                        AdapterId = this.Configuration.Id,
                        AdapterEntryId = GetItemId(fileSystemInfo)
                    });
            }).ConfigureAwait(false);
        }

        public override Stream GetReadStreamForEntry(SyncEntry entry)
        {
            return this.GetStreamForEntry(entry, false);
        }

        public override Stream GetWriteStreamForEntry(SyncEntry entry, long length)
        {
            return this.GetStreamForEntry(entry, true);
        }

        public Stream GetStreamForEntry(SyncEntry entry, bool isWrite)
        {
            if (entry.Type != SyncEntryType.File)
            {
                throw new InvalidOperationException("Cannot get a filestream for a non-file.");
            }

            string fullPath;
            using (var db = this.Relationship.GetDatabase())
            {
                fullPath = Path.Combine(this.Config.RootDirectory, entry.GetRelativePath(db, this.PathSeparator));
            }

            if (isWrite)
            {
                return File.Open(fullPath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            }

            return File.Open(fullPath, FileMode.Open, FileAccess.Read);
        }

        public override void UpdateItem(EntryUpdateInfo updateInfo, SyncEntryChangedFlags changeFlags)
        {
            string fullPath;
            using (var database = this.Relationship.GetDatabase())
            {
                fullPath = Path.Combine(this.Config.RootDirectory, updateInfo.Entry.GetRelativePath(database, this.PathSeparator));
            }

            FileSystemInfo fileSystemInfo = GetFileSystemInfo(fullPath, updateInfo.Entry.Type);

            if ((changeFlags & SyncEntryChangedFlags.CreatedTimestamp) != 0)
            {
                Pre.Assert(updateInfo.CreationDateTimeUtcNew != null, "updateInfo.CreationDateTimeUtcNew != null");

                // Write the new created timestamp to the file/folder
                fileSystemInfo.CreationTimeUtc = updateInfo.CreationDateTimeUtcNew.Value;

                // Update the SyncEntry to record that this is now the "current" value of CreationDateTimeUtcNew
                updateInfo.Entry.CreationDateTimeUtc = updateInfo.CreationDateTimeUtcNew.Value;
            }

            if ((changeFlags & SyncEntryChangedFlags.ModifiedTimestamp) != 0)
            {
                Pre.Assert(updateInfo.ModifiedDateTimeUtcNew != null, "updateInfo.ModifiedDateTimeUtcNew != null");

                // Write the new modified timestamp to the file/folder
                fileSystemInfo.LastWriteTimeUtc = updateInfo.ModifiedDateTimeUtcNew.Value;

                // Update the SyncEntry to record that this is now the "current" value of ModifiedDateTimeUtcNew
                updateInfo.Entry.ModifiedDateTimeUtc = updateInfo.ModifiedDateTimeUtcNew.Value;
            }
        }

        public override void DeleteItem(SyncEntry entry)
        {
            string fullPath;
            using (var database = this.Relationship.GetDatabase())
            {
                fullPath = Path.Combine(this.Config.RootDirectory, entry.GetRelativePath(database, this.PathSeparator));
            }

            switch (entry.Type)
            {
                case SyncEntryType.Directory:
                    Directory.Delete(fullPath);
                    break;
                case SyncEntryType.File:
                    File.Delete(fullPath);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            entry.EntryLastUpdatedDateTimeUtc = DateTime.UtcNow;
        }

        public override IEnumerable<IAdapterItem> GetAdapterItems(IAdapterItem folder)
        {
            if (folder == null)
            {
                DriveInfo[] allDrives = DriveInfo.GetDrives();

                List<IAdapterItem> folders = new List<IAdapterItem>();

                foreach (DriveInfo driveInfo in allDrives)
                {
                    try
                    {
                        folders.Add(FileSystemFolder.Create(driveInfo.RootDirectory, this, true));
                    }
                    catch (Exception exception)
                    {
                        Logger.Info(
                            "Failed to enumerate drive {0} ({1}). The error was: {2}", driveInfo.Name,
                            driveInfo.DriveType, exception.Message.Trim());
                    }
                }

                return folders;
            }

            FileSystemFolder fileSystemFolder = folder as FileSystemFolder;
            if (fileSystemFolder == null)
            {
                throw new InvalidOperationException("folder item is not a FileSystemFolder");
            }

            if (fileSystemFolder.ItemType != SyncAdapterItemType.Directory)
            {
                throw new InvalidOperationException("Invalid FileSystemFolder type");
            }

            return this.GetItemsFromDirectory((DirectoryInfo)fileSystemFolder.FileSystemInfo, folder);
        }

        public override bool IsEntryUpdated(SyncEntry childEntry, IAdapterItem adapterItem, out EntryUpdateResult result)
        {
            const long TicksPerMillisecond = 10000;

            // 2017/11/24: There appears to be a discrepency when reading ModifiedDateTimeUtc and CreationTimeUtc
            // from FileSystemInfo objects. The Ticks value is being rounded to the nearest 10000 ticks, causing 
            // some directories to appear to be modified. For now, we will set the threshold for an item being 
            // changed to 10ms
            const long Epsilon = TicksPerMillisecond * 10;

            FileSystemFolder fileSystemItem = adapterItem as FileSystemFolder;

            if (fileSystemItem == null)
            {
                throw new ArgumentException("The adapter item is not of the correct type.", nameof(adapterItem));
            }

            result = new EntryUpdateResult();

            if (Math.Abs(childEntry.ModifiedDateTimeUtc.Ticks - fileSystemItem.FileSystemInfo.LastWriteTimeUtc.Ticks) > Epsilon)
            {
                result.ChangeFlags |= SyncEntryChangedFlags.ModifiedTimestamp;
                result.ModifiedTime = fileSystemItem.FileSystemInfo.LastWriteTimeUtc;
            }

            if (Math.Abs(childEntry.CreationDateTimeUtc.Ticks - fileSystemItem.FileSystemInfo.CreationTimeUtc.Ticks) > Epsilon)
            {
                result.ChangeFlags |= SyncEntryChangedFlags.CreatedTimestamp;
                result.CreationTime = fileSystemItem.FileSystemInfo.CreationTimeUtc;
            }

            FileInfo fileInfo = fileSystemItem.FileSystemInfo as FileInfo;
            SyncEntryType fileType = SyncEntryType.Directory;
            if (fileInfo != null)
            {
                fileType = SyncEntryType.File;

                if (fileInfo.Length != childEntry.GetSize(this.Relationship, SyncEntryPropertyLocation.Source))
                {
                    result.ChangeFlags |= SyncEntryChangedFlags.FileSize;
                }
            }

            if (!string.Equals(fileSystemItem.Name, childEntry.Name, StringComparison.Ordinal))
            {
                result.ChangeFlags |= SyncEntryChangedFlags.Renamed;
            }

            // It is possible that a directory was created over a file that previously existed (with the same name). To 
            // handle this, we need to check if the type changed.
            if (childEntry.Type != fileType)
            {
                // TODO: Handle this
                throw new NotImplementedException();
            }

            return result.ChangeFlags != SyncEntryChangedFlags.None;
        }

        public override SyncEntry CreateSyncEntryForAdapterItem(IAdapterItem item, SyncEntry parentEntry)
        {
            FileSystemFolder fileSystemItem = item as FileSystemFolder;

            if (fileSystemItem == null)
            {
                throw new InvalidOperationException("Item type is incorrect.");
            }

            return this.CreateEntry(fileSystemItem.FileSystemInfo, parentEntry);
        }

        public override void FinalizeItemWrite(Stream stream, EntryUpdateInfo updateInfo)
        {
            stream.Flush();
            stream.Close();

            SyncEntryAdapterData adapterEntry = 
                updateInfo.Entry.AdapterEntries.FirstOrDefault(e => e.AdapterId == this.Config.Id);

            if (adapterEntry == null)
            {
                adapterEntry = new SyncEntryAdapterData()
                {
                    SyncEntry = updateInfo.Entry,
                    AdapterId = this.Configuration.Id,
                };

                updateInfo.Entry.AdapterEntries.Add(adapterEntry);
            }

            string fullPath;
            using (var db = this.Relationship.GetDatabase())
            {
                fullPath = Path.Combine(
                    this.Config.RootDirectory, 
                    updateInfo.Entry.GetRelativePath(db, this.PathSeparator));
            }

            adapterEntry.AdapterEntryId = GetItemId(fullPath, false);
        }

        private static readonly string[] SuppressedDirectories = { "$RECYCLE.BIN", "System Volume Information" };

        private IEnumerable<IAdapterItem> GetItemsFromDirectory(DirectoryInfo directory, IAdapterItem parent)
        {
            return
                directory.GetFileSystemInfos()
                    .Where(d => SuppressedDirectories.Contains(d.Name, StringComparer.OrdinalIgnoreCase) == false)
                    .Select(info => FileSystemFolder.Create(info, parent, this));
        }

        private SyncEntry CreateEntry(FileSystemInfo info, SyncEntry parentEntry)
        {
            SyncEntry entry = new SyncEntry
            {
                CreationDateTimeUtc = info.CreationTimeUtc,
                ModifiedDateTimeUtc = info.LastWriteTimeUtc,
                Name = info.Name,
                AdapterEntries = new List<SyncEntryAdapterData>()
            };

            if (parentEntry != null)
            {
                entry.AdapterEntries.Add(
                    new SyncEntryAdapterData()
                    {
                        AdapterId = this.Configuration.Id,
                        SyncEntry = entry,
                        AdapterEntryId = GetItemId(info)
                    });

                entry.ParentEntry = parentEntry;
                entry.ParentId = parentEntry.Id;
            }

            FileInfo fileInfo = info as FileInfo;

            if (fileInfo != null)
            {
                entry.Type = SyncEntryType.File;
                entry.SetSize(this.Relationship, SyncEntryPropertyLocation.Source, fileInfo.Length);
            }

            DirectoryInfo directoryInfo = info as DirectoryInfo;

            if (directoryInfo != null)
            {
                entry.Type = SyncEntryType.Directory;
            }

            if (entry.Type == SyncEntryType.Undefined)
            {
                throw new Exception("Unknown type for FileSystemInfo " + info.FullName);
            }

            entry.EntryLastUpdatedDateTimeUtc = DateTime.UtcNow;

            return entry;
        }

        private static FileSystemInfo GetFileSystemInfo(string fullPath, SyncEntryType type)
        {
            if (type == SyncEntryType.File)
            {
                return new FileInfo(fullPath);
            }

            return new DirectoryInfo(fullPath);
        }

        private static string GetItemId(FileSystemInfo info)
        {
            // TODO: Need to take into account testing on various file systems (FAT32, ReFS, etc)
            return GetItemId(info.FullName, (info.Attributes & FileAttributes.Directory) != 0);
        }

        private static string GetItemId(string fullName, bool isDirectory)
        {
            if (isDirectory)
            {
                // Item is a directory.
                return Convert.ToBase64String(NativeMethodHelpers.GetDirectoryObjectId(fullName));
            }

            return Convert.ToBase64String(NativeMethodHelpers.GetFileObjectId(fullName));
        }

        public WindowsFileSystemAdapter(SyncRelationship relationship) 
            : base(relationship, new WindowsFileSystemAdapterConfiguration())
        {
        }

        public WindowsFileSystemAdapter(SyncRelationship relationship, WindowsFileSystemAdapterConfiguration configuration) 
            : base(relationship, configuration)
        {
        }
    }

    public class WindowsFileSystemAdapterConfiguration : AdapterConfiguration
    {
        public override Guid AdapterTypeId => WindowsFileSystemAdapter.TargetTypeId;

        public string RootDirectory { get; set; }
    }
}