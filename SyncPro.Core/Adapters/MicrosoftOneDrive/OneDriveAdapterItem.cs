namespace SyncPro.Adapters.MicrosoftOneDrive
{
    using System;
    using System.Text;

    using SyncPro.Adapters.MicrosoftOneDrive.DataModel;

    public class OneDriveAdapterItem : AdapterItem, IChangeTrackedAdapterItem
    {
        public const string DefaultDriveName = "OneDrive";

        internal Drive Drive { get; set; }

        internal Item Item { get; set; }

        public bool IsDeleted { get; }
        public string ParentUniqueId { get; }

        internal OneDriveAdapterItem(Drive drive, AdapterBase adapter)
            : base(
                  DefaultDriveName, 
                  null, 
                  adapter, 
                  SyncAdapterItemType.Directory, 
                  drive.Id, 
                  0,
                  DateTime.MinValue,
                  DateTime.MinValue)
        {
            this.Drive = drive;
        }

        internal OneDriveAdapterItem(Item item, IAdapterItem parent, AdapterBase adapter)
            : base(
                  item.Name, 
                  parent, 
                  adapter, 
                  GetItemType(item), 
                  item.Id, 
                  item.Size,
                  item.CreatedDateTime.ToUniversalTime(),
                  (item.LastModifiedDateTime ?? DateTime.MinValue).ToUniversalTime())
        {
            this.Item = item;
            this.IsDeleted = item.Deleted != null;
            this.ParentUniqueId = item.ParentReference.Id;
        }

        private static SyncAdapterItemType GetItemType(Item item)
        {
            if (item.File != null)
            {
                return SyncAdapterItemType.File;
            }

            if (item.Folder != null)
            {
                return SyncAdapterItemType.Directory;
            }

            throw new NotImplementedException("Unknown item type");
        }

        public static byte[] ItemIdToUniqueId(string id)
        {
            return Encoding.ASCII.GetBytes(id);
        }
    }
}