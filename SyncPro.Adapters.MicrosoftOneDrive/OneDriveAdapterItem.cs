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

            if (item.File?.Hashes?.Sha1Hash != null)
            {
                this.Sha1Hash = StringToByteArrayFastest(item.File.Hashes.Sha1Hash);
            }
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

        public static byte[] StringToByteArrayFastest(string hex) 
        {
            if (hex.Length % 2 == 1)
                throw new Exception("The binary key cannot have an odd number of digits");

            byte[] arr = new byte[hex.Length >> 1];

            for (int i = 0; i < hex.Length >> 1; ++i)
            {
                arr[i] = (byte)((GetHexVal(hex[i << 1]) << 4) + (GetHexVal(hex[(i << 1) + 1])));
            }

            return arr;
        }

        public static int GetHexVal(char hex) {
            int val = (int)hex;
            //For uppercase A-F letters:
            return val - (val < 58 ? 48 : 55);
            //For lowercase a-f letters:
            //return val - (val < 58 ? 48 : 87);
            //Or the two combined, but a bit slower:
            //return val - (val < 58 ? 48 : (val < 97 ? 55 : 87));
        }
    }
}