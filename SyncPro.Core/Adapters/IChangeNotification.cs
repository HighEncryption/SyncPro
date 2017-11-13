namespace SyncPro.Adapters
{
    using System;
    using System.Collections.Generic;

    [Flags]
    public enum ItemChangeType
    {
        None = 0,
        Created = 0x01,
        Deleted = 0x02,
        Changed = 0x04,
        Renamed = 0x08,
        All = Renamed | Changed | Deleted | Created,
    }

    public class SyncEntryChange
    {
        public string FullName { get; set; }

        public ItemChangeType ChangeType { get; set; }
    }

    public class ItemChangedEventArgs : EventArgs
    {
        public List<SyncEntryChange> Changes { get; }

        public ItemChangedEventArgs()
        {
            this.Changes = new List<SyncEntryChange>();
        }
    }

    /// <summary>
    /// Defines an <see cref="AdapterBase"/> that supports change notification from the server, where an event will
    /// be raised by the <see cref="AdapterBase"/> when an item on the server has changed.
    /// </summary>
    public interface IChangeNotification
    {
        event EventHandler<ItemChangedEventArgs> ItemChanged;

        bool IsChangeNotificationEnabled { get; }

        void EnableChangeNotification(bool enabled);

        DateTime GetNextNotificationTime();
    }
}