﻿namespace SyncPro.Adapters.WindowsFileSystem
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    using JsonLog;

    using SyncPro.Configuration;
    using SyncPro.Data;
    using SyncPro.Runtime;

    public class WindowsFileSystemAdapter : AdapterBase
    {
        public static readonly Guid TargetTypeId = new Guid("b1755e86-381e-4e78-b47d-dbbfeee34585");

        // TODO: Need to implement this?
        // private CancellationTokenSource cancellationTokenSource;

        //public string RootDirectory { get; set; }

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

        public override void UpdateItem(SyncEntry entry, SyncEntryChangedFlags changeFlags)
        {
            string fullPath;
            using (var database = this.Relationship.GetDatabase())
            {
                fullPath = Path.Combine(this.Config.RootDirectory, entry.GetRelativePath(database, this.PathSeparator));
            }

            FileSystemInfo f = GetFileSystemInfo(fullPath, entry.Type);

            switch (changeFlags)
            {
                case SyncEntryChangedFlags.ModifiedTimestamp:
                    if (this.Relationship.Configuration.SyncTimestamps)
                    {
                        f.LastWriteTimeUtc = entry.ModifiedDateTimeUtc;
                    }
                    break;
                case SyncEntryChangedFlags.CreatedTimestamp:
                    if (this.Relationship.Configuration.SyncTimestamps)
                    {
                        f.CreationTimeUtc = entry.CreationDateTimeUtc;
                    }
                    break;
                case SyncEntryChangedFlags.Renamed:
                    throw new NotImplementedException();
            }
        }

        public override void DeleteItem(SyncEntry entry)
        {
            string fullPath;
            using (var database = this.Relationship.GetDatabase())
            {
                fullPath = Path.Combine(this.Config.RootDirectory, entry.GetRelativePath(database, this.PathSeparator));

                //// Here we need to call GetRelativePathStack instead of just GetRelativePath because the path may contain a 
                //// different separator character, and we need to ensure that the seperator character is a '\' for use in this
                //// adapter's delete logic.
                //IList<string> entryPathStack = entry.GetRelativePathStack(database);
                //fullPath = Path.Combine(this.RootDirectory, string.Join(this.PathSeparator, entryPathStack));
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

        public override bool IsEntryUpdated(SyncEntry childEntry, IAdapterItem adapterItem, out SyncEntryChangedFlags changeFlags)
        {
            const long TicksPerMillisecond = 10000;
            const long Epsilon = TicksPerMillisecond * 2;

            FileSystemFolder fileSystemItem = adapterItem as FileSystemFolder;

            if (fileSystemItem == null)
            {
                throw new ArgumentException("The adapter item is not of the correct type.", nameof(adapterItem));
            }

            changeFlags = SyncEntryChangedFlags.None;

            if (this.Relationship.Configuration.SyncTimestamps)
            {
                if (Math.Abs(childEntry.ModifiedDateTimeUtc.Ticks - fileSystemItem.FileSystemInfo.LastWriteTimeUtc.Ticks) > Epsilon)
                {
                    changeFlags |= SyncEntryChangedFlags.ModifiedTimestamp;
                }

                if (Math.Abs(childEntry.CreationDateTimeUtc.Ticks - fileSystemItem.FileSystemInfo.CreationTimeUtc.Ticks) > Epsilon)
                {
                    changeFlags |= SyncEntryChangedFlags.CreatedTimestamp;
                }
            }

            FileInfo fileInfo = fileSystemItem.FileSystemInfo as FileInfo;
            SyncEntryType fileType = SyncEntryType.Directory;
            if (fileInfo != null)
            {
                fileType = SyncEntryType.File;

                if (fileInfo.Length != childEntry.Size)
                {
                    changeFlags |= SyncEntryChangedFlags.FileSize;
                }
            }

            if (!string.Equals(fileSystemItem.Name, childEntry.Name, StringComparison.Ordinal))
            {
                changeFlags |= SyncEntryChangedFlags.Renamed;
            }

            // It is possible that a directory was created over a file that previously existed (with the same name). To 
            // handle this, we need to check if the type changed.
            if (childEntry.Type != fileType)
            {
                // TODO: Handle this
                throw new NotImplementedException();
            }

            return changeFlags != SyncEntryChangedFlags.None;
        }

        public override SyncEntry CreateSyncEntryForAdapterItem(IAdapterItem item, SyncEntry parentEntry)
        {
            FileSystemFolder fileSystemItem = item as FileSystemFolder;

            if (fileSystemItem == null)
            {
                throw new InvalidOperationException("Item type is incorrect.");
            }

            //return this.CreateEntryForFileInfo(fileSystemItem.FileSystemInfo, parentEntry);
            return this.CreateEntry(fileSystemItem.FileSystemInfo, parentEntry);
        }

        //private SyncEntry CreateEntryForFileInfo(FileSystemInfo info, SyncEntry parentEntry)
        //{
        //    SyncEntry entry = new SyncEntry
        //    {
        //        CreationDateTimeUtc = info.CreationTimeUtc,
        //        ModifiedDateTimeUtc = info.LastWriteTimeUtc,
        //        Name = info.Name,
        //    };

        //    entry.AdapterEntries.Add(new SyncEntryAdapterData()
        //    {
        //        Adapter = this.Configuration,
        //        SyncEntry = entry,
        //        AdapterEntryId = GetItemId(info)
        //    });

        //    if (parentEntry != null)
        //    {
        //        entry.ParentEntry = parentEntry;
        //        entry.ParentId = parentEntry.Id;
        //    }

        //    FileInfo fileInfo = info as FileInfo;

        //    if (fileInfo != null)
        //    {
        //        entry.Type = SyncEntryType.File;
        //        entry.Size = fileInfo.Length;

        //        // TODO: Compute this when we actually copy the file
        //        //entry.Sha1Hash = ComputerSha1Hash(info.FullName);
        //    }

        //    DirectoryInfo directoryInfo = info as DirectoryInfo;

        //    if (directoryInfo != null)
        //    {
        //        entry.Type = SyncEntryType.Directory;
        //    }

        //    if (entry.Type == SyncEntryType.Undefined)
        //    {
        //        throw new Exception("Unknown type for FileSystemInfo " + info.FullName);
        //    }

        //    if (this.Relationship.Configuration.SyncTimestamps)
        //    {
        //        entry.CreationDateTimeUtc = info.CreationTimeUtc;
        //        entry.ModifiedDateTimeUtc = info.LastWriteTimeUtc;
        //    }

        //    entry.EntryLastUpdatedDateTimeUtc = DateTime.UtcNow;

        //    return entry;
        //}

        // TODO: Rewrite this as an members in an include/exclude list
        private static readonly string[] SuppressedDirectories = { "$RECYCLE.BIN", "System Volume Information" };

        private IEnumerable<IAdapterItem> GetItemsFromDirectory(DirectoryInfo directory, IAdapterItem parent)
        {
            return
                directory.GetFileSystemInfos()
                    .Where(d => SuppressedDirectories.Contains(d.Name, StringComparer.OrdinalIgnoreCase) == false)
                    .Select(info => FileSystemFolder.Create(info, parent, this));
        }

        //public override void LoadConfiguration()
        //{
        //    JObject json = JObject.Parse(this.Configuration.CustomConfiguration);
        //    this.RootDirectory = Convert.ToString(json["RootDirectory"]);
        //}

        //public override byte[] GetUniqueId(SyncEntry entry)
        //private static byte[] GetUniqueId(SyncEntry entry)
        //{
        //    string fullPath;
        //    using (var db = this.Relationship.GetDatabase())
        //    {
        //        fullPath = Path.Combine(this.RootDirectory, entry.GetRelativePath(db, this.PathSeparator));
        //    }

        //    FileSystemInfo f = GetFileSystemInfo(fullPath, entry.Type);
        //    return GetItemId(f);
        //}

        //public override void SaveConfiguration()
        //{
        //    JObject json = new JObject();
        //    if (!string.IsNullOrWhiteSpace(this.Configuration.CustomConfiguration))
        //    {
        //        json = JObject.Parse(this.Configuration.CustomConfiguration);
        //    }

        //    json["RootDirectory"] = this.RootDirectory;
        //    this.Configuration.CustomConfiguration = json.ToString(Formatting.None);
        //}

        private SyncEntry CreateEntry(FileSystemInfo info, SyncEntry parentEntry)
        {
            SyncEntry entry = new SyncEntry
            {
                CreationDateTimeUtc = info.CreationTimeUtc,
                ModifiedDateTimeUtc = info.LastWriteTimeUtc,
                Name = info.Name
            };

            entry.AdapterEntries = new List<SyncEntryAdapterData>();

            if (parentEntry != null)
            {
                entry.AdapterEntries.Add(
                    new SyncEntryAdapterData()
                    {
                        //Adapter = this.Configuration,
                        AdapterId = this.Configuration.Id,
                        SyncEntry = entry,
                        AdapterEntryId = GetItemId(info)
                    });
            }

            if (parentEntry != null)
            {
                entry.ParentEntry = parentEntry;
                entry.ParentId = parentEntry.Id;
            }

            FileInfo fileInfo = info as FileInfo;

            if (fileInfo != null)
            {
                entry.Type = SyncEntryType.File;
                entry.Size = fileInfo.Length;

                // TODO: Compute this when we actually copy the file
                //entry.Sha1Hash = ComputerSha1Hash(info.FullName);
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

            if (this.Relationship.Configuration.SyncTimestamps)
            {
                entry.CreationDateTimeUtc = info.CreationTimeUtc;
                entry.ModifiedDateTimeUtc = info.LastWriteTimeUtc;
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
            if (info.Attributes.HasFlag(FileAttributes.Directory))
            {
                // Item is a directory.
                return Convert.ToBase64String(NativeMethodHelpers.GetDirectoryObjectId(info.FullName));
            }
            else
            {
                return Convert.ToBase64String(NativeMethodHelpers.GetFileObjectId(info.FullName));
            }
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