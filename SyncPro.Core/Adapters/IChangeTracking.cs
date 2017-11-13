namespace SyncPro.Adapters
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines an <see cref="AdapterBase"/> that supports change tracking
    /// </summary>
    /// <remarks>
    /// Change tracking is the ability for an <see cref="AdapterBase"/> to be able to query the server for a set of
    /// changes that have occurred since the last time the <see cref="AdapterBase"/> queried the server. The exact
    /// implementation of change tracking is adapter-dependent, but in general allows the adapter to as for the 
    /// 'delta set' of changes items.
    /// </remarks>
    public interface IChangeTracking
    {
        /// <summary>
        /// Retrieve the current set of changes that have occured since the last time changes were committed. 
        /// </summary>
        /// <remarks>
        /// Adapters that implement this interface are responsible for tracking these changes (usually done via a delta 
        /// token provided by the service.
        /// </remarks>
        /// <returns>
        /// A <see cref="TrackedChange"/> object that contains information about the set of changes that have occurred.
        /// </returns>
        Task<TrackedChange> GetChangesAsync();

        Task CommitChangesAsync(TrackedChange trackedChange);

        /// <summary>
        /// Indicates whether the adapter currently has tracked state. 
        /// </summary>
        /// <remarks>
        /// This is typically indicated by whether the adapter has a valid delta token.
        /// </remarks>
        bool HasTrackedState { get; }
    }

    public class TrackedChange
    {
        /// <summary>
        /// The internal state used to track this set of changes
        /// </summary>
        /// <remarks>
        /// Typically this is a value provided by the adapter that allows the service (i.e. OneDrive) to be 
        /// queried for a set fo changes since the last state was synced. This way, we can get the set of
        /// change that have occurred since the last sync without having to enumerate all of the files on
        /// the adapter (saving time/bandwidth/processing).
        /// </remarks>
        public string State { get; }

        /// <summary>
        /// The list of all changes found for the associated adapter
        /// </summary>
        public List<IChangeTrackedAdapterItem> Changes { get; }

        public TrackedChange(string state)
        {
            this.State = state;
            this.Changes = new List<IChangeTrackedAdapterItem>();
        }
    }

    public interface IChangeTrackedAdapterItem : IAdapterItem
    {
        bool IsDeleted { get; }

        string ParentUniqueId { get; }
    }
}