namespace SyncPro.Adapters
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

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

    /// <summary>
    /// Represents a change that has occurred to an item
    /// </summary>
    public class ItemChange
    {
        /// <summary>
        /// The full name (fully qualified name) of the item.
        /// </summary>
        public string FullName { get; set; }

        /// <summary>
        /// The type of changes that occurred.
        /// </summary>
        public ItemChangeType ChangeType { get; set; }

        public ItemChange()
        {
        }

        public ItemChange(string fullName, ItemChangeType changeType)
        {
            this.FullName = fullName;
            this.ChangeType = changeType;
        }
    }

    /// <summary>
    /// EventArgs for the ItemsChangedEventArgs event.
    /// </summary>
    public class ItemsChangedEventArgs : EventArgs
    {
        public ItemsChangedEventArgs()
        {
            this.Changes = new List<ItemChange>();
        }

        public List<ItemChange> Changes { get; }
    }

    /// <summary>
    /// Defines an <see cref="AdapterBase"/> that supports change notification from the server, where an event will
    /// be raised by the <see cref="AdapterBase"/> when an item on the server has changed.
    /// </summary>
    public interface IChangeNotification
    {
        /// <summary>
        /// This event is raised when an adapter detects that items have changed, typically via an asynchronous 
        /// notification from the underlying service.
        /// </summary>
        event EventHandler<ItemsChangedEventArgs> ItemChanged;

        /// <summary>
        /// Gets a value indicating whether change notification is currently enabled.
        /// </summary>
        bool IsChangeNotificationEnabled { get; }

        /// <summary>
        /// Enabled or disables change notification
        /// </summary>
        /// <param name="enabled">Whether change notification is enabled.</param>
        void EnableChangeNotification(bool enabled);

        /// <summary>
        /// Gets the datetime of the next time when the adapter will be checked for changes.
        /// </summary>
        /// <returns>The next notification time</returns>
        DateTime GetNextNotificationTime();
    }

    public interface IThumbnails
    {
        Task<byte[]> GetItemThumbnail(string itemId, string relativePath);
    }
}