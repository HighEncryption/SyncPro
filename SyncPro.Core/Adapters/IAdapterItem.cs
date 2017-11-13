namespace SyncPro.Adapters
{
    public enum SyncAdapterItemType
    {
        Undefined,
        Directory,
        File
    }

    /// <summary>
    /// Represents an item exposed by an adapter, such as a file or directory.
    /// </summary>
    /// <remarks>
    /// The sync and analyze components require that any item exposed by an adapter (such as a file or 
    /// directory) implement the <see cref="IAdapterItem"/> interface. Each adapter type should declare
    /// its own adapter item class that implements this interface.
    /// </remarks>
    public interface IAdapterItem
    {
        /// <summary>
        /// The discrete name of the item (e.g. file name or directory name).
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The unique ID of the item.
        /// </summary>
        /// <remarks>
        /// Each adapter type can implement this depending on its own needs, however the following rules 
        /// must be followed:
        /// - UniqueIds CAN be any value, so long as they are unique and non-null.
        /// - UniqueIds MUST be globally unique within the scope of an adapter. Two items cannot have the 
        ///   same UniqueId value.
        /// - UniqueIds SHOULD be consistent for the lifetime of an item. A change in a UniqueId will result
        ///   in and item being re-synchronized.
        /// - UniqueIds can have a maximum length of 128 bytes. Anything longer will be rejected by the DB.
        /// </remarks>
        string UniqueId { get; }

        /// <summary>
        /// The type of the item (either a directory or a file).
        /// </summary>
        SyncAdapterItemType ItemType { get; }

        /// <summary>
        /// The full path of the item.
        /// </summary>
        /// <remarks>
        /// For performance reasons, this value should be calculated at runtime. Lower memory usage is favored 
        /// over CPU usage.
        /// </remarks>
        string FullName { get; }

        long Size { get; }

        /// <summary>
        /// The adapter item that is the parent of this item.
        /// </summary>
        IAdapterItem Parent { get; }

        /// <summary>
        /// The adapter where this item originated from.
        /// </summary>
        AdapterBase Adapter { get; }

        string ErrorMessage { get; }
    }
}