namespace SyncPro.Adapters
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    using SyncPro.Configuration;
    using SyncPro.Data;
    using SyncPro.Runtime;

    public enum AdapterLocality
    {
        LocalComputer,
        LocalNetwork,
        Internet
    }

    /// <summary>
    /// Base class for all adapters.
    /// </summary>
    public abstract class AdapterBase : NotifyPropertyChangedSlim
    {
        /// <summary>
        /// Gets the relationship that the adapter belongs to.
        /// </summary>
        public SyncRelationship Relationship { get; }

        /// <summary>
        /// Gets the persisted configuration for this adapter
        /// </summary>
        public AdapterConfiguration Configuration { get; protected set; }

        /// <summary>
        /// Indicates whether the adapter is in a Faulted state.
        /// </summary>
        public bool IsFaulted => this.FaultInformation != null;

        private AdapterFaultInformation faultInformation;

        /// <summary>
        /// Gets the fault information for the adapter.
        /// </summary>
        public AdapterFaultInformation FaultInformation
        {
            get => this.faultInformation;
            set
            {
                if (this.SetProperty(ref this.faultInformation, value))
                {
                    this.RaisePropertyChanged(nameof(this.IsFaulted));
                }
            }
        }

        /// <summary>
        /// Gets the <see cref="SyncEntry"/> that represents the root of the adapter's item tree.
        /// </summary>
        public SyncEntry GetRootSyncEntry()
        {
            using (var db = this.Relationship.GetDatabase())
            {
                return db.Entries.FirstOrDefault(e => e.Id == this.Configuration.RootIndexEntryId);
            }
        }

        public virtual string PathSeparator => "\\";

        /// <summary>
        /// Gets the GUID that uniquely identifies this type of adapter.
        /// </summary>
        public abstract Guid GetTargetTypeId();

        public virtual AdapterLocality Locality => AdapterLocality.Internet;

        /// <summary>
        /// Created the root SyncEntry object for this adapter.
        /// </summary>
        /// <remarks>
        /// This method will only create the SyncEntry that represents the root of the folder structure to be synchronized
        /// and set it in the adapter's configuration. It will not add the object to the database.
        /// </remarks>
        public abstract Task<SyncEntry> CreateRootEntry();

        /// <summary>
        /// Initialize an existing adapter. This method is called when an adapter is created for the first time and after an 
        /// existing adapter is loaded from the database.
        /// </summary>
        public virtual Task InitializeAsync()
        {
            return Task.FromResult(default(object));
        }

        /// <summary>
        /// Gets the <see cref="IAdapterItem"/> that represents the root of the adapter's item tree.
        /// </summary>
        public abstract Task<IAdapterItem> GetRootFolder();

        /// <summary>
        /// Create the item (file/directory) on the adapter as represented by the entity.
        /// </summary>
        /// <remarks>
        /// The item should only be created and the metadata set correctly (timestamps, etc.). In the case of a file, the
        /// contents of the file should NOT be copied as a part of this function. Also, the implemented method must update
        /// the adapter data on the entry.
        /// </remarks>
        public abstract Task CreateItemAsync(SyncEntry entry);

        /// <summary>
        /// Get a stream for reading the contents of an item
        /// </summary>
        public abstract Stream GetReadStreamForEntry(SyncEntry entry);

        /// <summary>
        /// Get a stream for writing the contents of an item
        /// </summary>
        public abstract Stream GetWriteStreamForEntry(SyncEntry entry, long length);

        /// <summary>
        /// Finish the writing of a stream for an item.
        /// </summary>
        /// <param name="stream">The stream used to write to the item</param>
        /// <param name="updateInfo">The <see cref="EntryUpdateInfo"/> identifying the item being written</param>
        /// <remarks>
        /// The purpose of this method is to allow the adapter to perform any finalization/cleanup of an entry
        /// after the contents of the entry have been written but before the stream itself has been closed. This
        /// can be useful in situations where the adapter need to write additional data to the stream prior to 
        /// closing it, and so that these writes to not need to occur as a part of the stream's disposure.
        /// </remarks>
        public abstract void FinalizeItemWrite(Stream stream, EntryUpdateInfo updateInfo);

        /// <summary>
        /// Update the metadata (creation timestamp modified timestamp, etc) of an item
        /// </summary>
        /// <param name="updateInfo">The <see cref="EntryUpdateInfo"/> containing the values to be written</param>
        /// <param name="changeFlags">The flags indicating which values need to be written</param>
        /// <remarks>
        /// After updating each of the metadata properties, the method MUST also set the value on the updateInfo.Entry
        /// object to record that the value as been written.
        /// </remarks>
        public abstract void UpdateItem(EntryUpdateInfo updateInfo, SyncEntryChangedFlags changeFlags);

        /// <summary>
        /// Delete an item from the adapter identified by the entity. The entity for the item will not be deleted/
        /// </summary>
        public abstract void DeleteItem(SyncEntry entry);

        /// <summary>
        /// Get the items (files and directories) under a folder.
        /// </summary>
        public abstract IEnumerable<IAdapterItem> GetAdapterItems(IAdapterItem folder);

        /// <summary>
        /// Determines if an adapter item has changed.
        /// </summary>
        /// <param name="childEntry">The currently known state of the item (from the database)</param>
        /// <param name="adapterItem">The currently known state of the item (from the adapter)</param>
        /// <param name="result">Result containing metadata about the change</param>
        /// <returns>True if the item has changed, false otherwise</returns>
        /// <remarks>Note that this call performs an in-memory check and MUST NOT result in a network/disk call.</remarks>
        public abstract bool IsEntryUpdated(SyncEntry childEntry, IAdapterItem adapterItem, out EntryUpdateResult result);

        public abstract SyncEntry CreateSyncEntryForAdapterItem(IAdapterItem item, SyncEntry parentEntry);

        public virtual void SaveConfiguration() { }

        public virtual void LoadConfiguration() { }

        public event EventHandler<SyncEntryChangedEventArgs> SyncEntryChanged;

        protected void RaiseSyncEntryChanged(EntryUpdateInfo updateInfo)
        {
            this.SyncEntryChanged?.Invoke(this, new SyncEntryChangedEventArgs(updateInfo));
        }

        protected AdapterBase(SyncRelationship relationship, AdapterConfiguration configuration)
        {
            this.Relationship = relationship;
            this.Configuration = configuration;
        }

        public static byte[] HexToBytes(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return new byte[0];
            }

            byte[] bytes = new byte[input.Length / 2];
            for (int i = 0; i < input.Length; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(input.Substring(i, 2), 16);
            }

            return bytes;
        }

        /// <summary>
        /// Indicates whether the <see cref="AdapterBase"/> supports change notification. See 
        /// <see cref="IChangeNotification"/> for more information on change notifiation.
        /// </summary>
        public bool SupportsChangeNotification()
        {
            return this is IChangeNotification;
        }

        /// <summary>
        /// Indicates whether the <see cref="AdapterBase"/> supports change tracking. See 
        /// <see cref="IChangeTracking"/> for more information on change notifiation.
        /// </summary>
        public bool SupportChangeTracking()
        {
            return this is IChangeTracking;
        }

        public abstract byte[] GetItemHash(HashType hashType, IAdapterItem adapterItem);
    }

    public class EntryUpdateResult
    {
        public SyncEntryChangedFlags ChangeFlags { get; set; }

        public DateTime CreationTime { get; set; }

        public DateTime ModifiedTime { get; set; }
    }

    public class SyncEntryChangedEventArgs
    {
        public SyncEntryChangedEventArgs(EntryUpdateInfo updateInfo)
        {
            this.UpdateInfo = updateInfo;
        }

        public EntryUpdateInfo UpdateInfo { get; set; }
    }
}
