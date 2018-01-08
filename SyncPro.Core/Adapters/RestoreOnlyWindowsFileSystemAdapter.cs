namespace SyncPro.Adapters
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    using SyncPro.Configuration;
    using SyncPro.Data;
    using SyncPro.Runtime;

    public class RestoreOnlyWindowsFileSystemAdapter : AdapterBase
    {
        public static readonly Guid TargetTypeId = new Guid("a7e04307-efa5-43d9-8126-4ee0ed09171b");

        public RestoreOnlyWindowsFileSystemAdapterConfiguration Config =>
            (RestoreOnlyWindowsFileSystemAdapterConfiguration)this.Configuration;

        public RestoreOnlyWindowsFileSystemAdapter(
            SyncRelationship relationship,
            AdapterConfiguration configuration)
            : base(relationship, configuration)
        {
        }

        public override Guid GetTargetTypeId()
        {
            return TargetTypeId;
        }

        public override Task<SyncEntry> CreateRootEntry()
        {
            throw new NotImplementedException();
        }

        public override Task<IAdapterItem> GetRootFolder()
        {
            throw new NotImplementedException();
        }

        public override Task CreateItemAsync(SyncEntry entry)
        {
            throw new NotImplementedException();
        }

        public override Stream GetReadStreamForEntry(SyncEntry entry)
        {
            throw new NotImplementedException();
        }

        public override Stream GetWriteStreamForEntry(SyncEntry entry, long length)
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

            return File.Open(fullPath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
        }

        public override void UpdateItem(EntryUpdateInfo updateInfo, SyncEntryChangedFlags changeFlags)
        {
            throw new NotImplementedException();
        }

        public override void DeleteItem(SyncEntry entry)
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<IAdapterItem> GetAdapterItems(IAdapterItem folder)
        {
            throw new NotImplementedException();
        }

        public override bool IsEntryUpdated(SyncEntry childEntry, IAdapterItem adapterItem, out EntryUpdateResult result)
        {
            throw new NotImplementedException();
        }

        public override SyncEntry CreateSyncEntryForAdapterItem(IAdapterItem item, SyncEntry parentEntry)
        {
            throw new NotImplementedException();
        }

        public override void FinalizeItemWrite(Stream stream, EntryUpdateInfo updateInfo)
        {
            throw new NotImplementedException();
        }
    }
}