using System;

namespace SyncPro.Adapters
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    using SyncPro.Adapters.GoogleDrive;
    using SyncPro.Adapters.MicrosoftOneDrive;
    using SyncPro.Adapters.WindowsFileSystem;
    using SyncPro.Configuration;
    using SyncPro.Data;
    using SyncPro.Runtime;

    public class SyncEntryChangedEventArgs
    {
        public SyncEntryChangedEventArgs(EntryUpdateInfo updateInfo)
        {
            this.UpdateInfo = updateInfo;
        }

        public EntryUpdateInfo UpdateInfo { get; set; }
    }

    /// <summary>
    /// Base class for all adapters.
    /// </summary>
    public abstract class AdapterBase : NotifyPropertyChangedSlim
    {
        public SyncRelationship Relationship { get; }

        public AdapterConfiguration Configuration { get; protected set; }

        public bool IsFaulted => this.FaultInformation != null;

        private AdapterFaultInformation faultInformation;

        public AdapterFaultInformation FaultInformation
        {
            get { return this.faultInformation; }
            set
            {
                if (this.SetProperty(ref this.faultInformation, value))
                {
                    this.RaisePropertyChanged(nameof(this.IsFaulted));
                }
            }
        }

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

        ///// <summary>
        ///// Gets the human-readable display string for this adapter.
        ///// </summary>
        //public abstract string GetDisplayString();

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
        /// Get the stream for the contents of the item
        /// </summary>
        public abstract Stream GetReadStreamForEntry(SyncEntry entry);

        public abstract Stream GetWriteStreamForEntry(SyncEntry entry, long length);

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

        //public abstract byte[] GetUniqueId(SyncEntry entry);

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
    }

    public class EntryUpdateResult
    {
        public SyncEntryChangedFlags ChangeFlags { get; set; }

        public DateTime CreationTime { get; set; }

        public DateTime ModifiedTime { get; set; }
    }

    public sealed class AdapterFactory
    {
        public static AdapterBase CreateFromConfig(AdapterConfiguration config, SyncRelationship relationship)
        {
            if (config.AdapterTypeId == WindowsFileSystemAdapter.TargetTypeId)
            {
                return new WindowsFileSystemAdapter(
                    relationship, 
                    (WindowsFileSystemAdapterConfiguration)config);
            }

            if (config.AdapterTypeId == OneDriveAdapter.TargetTypeId)
            {
                return new OneDriveAdapter(
                    relationship, 
                    (OneDriveAdapterConfiguration)config);
            }

            throw new NotImplementedException("Unknown adapter type " + config.AdapterTypeId);
        }

        public static Type GetTypeFromAdapterTypeId(Guid typeId)
        {
            if (typeId == WindowsFileSystemAdapter.TargetTypeId)
            {
                return typeof(WindowsFileSystemAdapter);
            }

            if (typeId == OneDriveAdapter.TargetTypeId)
            {
                return typeof(OneDriveAdapter);
            }

            if (typeId == GoogleDriveAdapter.TargetTypeId)
            {
                return typeof(GoogleDriveAdapter);
            }

            return null;
        }
    }
}