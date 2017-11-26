namespace SyncPro.Adapters.GoogleDrive
{
    using System.Text;

    using SyncPro.Adapters.GoogleDrive.DataModel;

    public class GoogleDriveAdapterItem : AdapterItem
    {
        public const string DefaultDriveName = "My Drive";

        internal Item Item { get; set; }

        internal GoogleDriveAdapterItem(Item item, IAdapterItem parent, AdapterBase adapter)
            : base(
                  item.Name, 
                  parent, 
                  adapter, 
                  GetItemType(item), 
                  item.Id, 
                  item.Size,
                  item.CreatedTime,
                  item.ModifiedTime)
        {
            this.Item = item;
        }

        private static SyncAdapterItemType GetItemType(Item item)
        {
            if (item.IsFolder)
            {
                return SyncAdapterItemType.Directory;
            }

            return SyncAdapterItemType.File;
        }

        public static byte[] ItemIdToUniqueId(string id)
        {
            return Encoding.ASCII.GetBytes(id);
        }
    }
}