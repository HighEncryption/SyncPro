namespace SyncPro.Adapters.MicrosoftOneDrive.DataModel
{
    using System;

    /// <summary>
    /// A container class to provide a little syntactic sugar so that drives and folders
    /// can be used interchangeably.
    /// </summary>
    public class ItemContainer
    {
        private readonly Drive drive;

        public Drive Drive
        {
            get
            {
                if (this.IsItem)
                {
                    throw new InvalidOperationException("ItemContainer is not of type Drive.");
                }

                return this.drive;
            }
        }

        private readonly Item item;

        public Item Item
        {
            get
            {
                if (!this.IsItem)
                {
                    throw new InvalidOperationException("ItemContainer is not of type Item.");
                }

                return this.item;
            }
        }

        /// <summary>
        /// True if the contain is a folder, false if it is a drive.
        /// </summary>
        public bool IsItem { get; }

        public ItemContainer(Drive drive)
        {
            this.drive = drive;
            this.IsItem = false;
        }

        public ItemContainer(Item item)
        {
            this.item = item;
            this.IsItem = true;
        }

        public static implicit operator ItemContainer(Drive drive)
        {
            return new ItemContainer(drive);
        }

        public static implicit operator ItemContainer(Item item)
        {
            return new ItemContainer(item);
        }

        public static implicit operator ItemContainer(OneDriveAdapterItem item)
        {
            if (item.Drive != null)
            {
                return new ItemContainer(item.Drive);
            }

            if (item.Item != null)
            {
                return new ItemContainer(item.Item);
            }

            throw new InvalidOperationException();
        }

        public static ItemContainer FromAdapterItem(OneDriveAdapterItem rootFolder)
        {
            if (rootFolder.Drive != null)
            {
                return new ItemContainer(rootFolder.Drive);
            }

            return new ItemContainer(rootFolder.Item);
        }
    }
}