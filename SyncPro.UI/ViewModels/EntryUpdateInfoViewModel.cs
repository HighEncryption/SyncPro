namespace SyncPro.UI.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Windows.Media;

    using SyncPro.Adapters;
    using SyncPro.Data;
    using SyncPro.Runtime;
    using SyncPro.UI.Framework;
    using SyncPro.UI.Utility;

    /// <summary>
    /// ViewModel wrapper for the <see cref="EntryUpdateInfo"/> class, which contains 
    /// information about the changes being made to an entry.
    /// </summary>
    public class EntryUpdateInfoViewModel : ViewModelBase, ISyncEntryMetadataChange
    {

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string name;

        /// <summary>
        /// The name of the entry (file or folder name) being changed
        /// </summary>
        public string Name
        {
            get { return this.name; }
            set { this.SetProperty(ref this.name, value); }
        }

        public string RelativePath { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool isDirectory;

        /// <summary>
        /// Whether or not the entry is a directory
        /// </summary>
        public bool IsDirectory
        {
            get { return this.isDirectory; }
            set { this.SetProperty(ref this.isDirectory, value); }
        }

        /// <summary>
        /// A display string containing a comma-separated list of the change flags (NewFile, Sha1Hash, etc)
        /// </summary>
        public string ChangeHeader { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool noChange;

        public bool NoChange
        {
            get { return this.noChange; }
            set { this.SetProperty(ref this.noChange, value); }
        }

        public bool IsNewItem { get; }

        public bool IsUpdatedItem { get; }

        public bool IsDeletedItem { get; }

        // The region below is the implementation of the ISyncEntryMetadataChange members
        // for tracking the metadata changes for a sync entry. If there are any missing 
        // members, simply copy this same block from the SyncHistoryEntryData class.
        #region Metadata Properties

        /// <summary>
        /// The previous size in bytes of the entry (if changed)
        /// </summary>
        public long SizeOld { get; set; }

        /// <summary>
        /// The size of the entry (in bytes) at the time when it was synced.
        /// </summary>
        public long SizeNew { get; set; }

        /// <summary>
        /// The previous SHA1 Hash of the file content (if changed)
        /// </summary>
        public byte[] Sha1HashOld { get; set; }

        /// <summary>
        /// The SHA1 Hash of the file content at the time when it was synced.
        /// </summary>
        public byte[] Sha1HashNew { get; set; }

        /// <summary>
        /// The previous MD5 Hash of the file content (if changed)
        /// </summary>
        public byte[] Md5HashOld { get; set; }

        /// <summary>
        /// The MD5 Hash of the file content at the time when it was synced.
        /// </summary>
        public byte[] Md5HashNew { get; set; }

        /// <summary>
        /// The previous CreationTime of the entry (if changed)
        /// </summary>
        public DateTime? CreationDateTimeUtcOld { get; set; }

        /// <summary>
        /// The CreationTime of the entry at the time it was synced.
        /// </summary>
        public DateTime? CreationDateTimeUtcNew { get; set; }

        /// <summary>
        /// The previous ModifiedTime of the entry (if changed)
        /// </summary>
        public DateTime? ModifiedDateTimeUtcOld { get; set; }

        /// <summary>
        /// The ModifiedTime of the entry at the time it was synced.
        /// </summary>
        public DateTime? ModifiedDateTimeUtcNew { get; set; }

        /// <summary>
        /// The previous full path of the item (from the root of the adapter) if changed.
        /// </summary>
        public string PathOld { get; set; }

        /// <summary>
        /// The full path of the item (from the root of the adapter) when it was synced.
        /// </summary>
        public string PathNew { get; set; }

        #endregion


        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool isExpanded;

        public bool IsExpanded
        {
            get { return this.isExpanded; }
            set { this.SetProperty(ref this.isExpanded, value); }
        }

        private string typeName;

        public string TypeName
        {
            get
            {
                if (this.typeName == null)
                {
                    if (this.IsDirectory)
                    {
                        this.typeName = "Folder";
                    }

                    FileInfo fileInfo = FileInfoCache.GetFileInfo(this.Name.ToLowerInvariant());
                    this.typeName = fileInfo.TypeName;
                }

                return this.typeName;
            }
        }

        private ImageSource largeIcon;

        public ImageSource LargeIcon
        {
            get
            {
                if (this.typeName == null)
                {
                    if (this.IsDirectory)
                    {
                        this.typeName = "Folder";
                    }

                    FileInfo fileInfo = FileInfoCache.GetFileInfo(this.Name.ToLowerInvariant());
                    this.largeIcon = fileInfo.LargeIcon;
                }

                return this.largeIcon;
            }
        }


        public EntryUpdateInfoViewModel(EntryUpdateInfo info)
        {
            this.Name = info.Entry.Name;
            this.RelativePath = info.RelativePath;

            this.IsDirectory = info.Entry.Type == SyncEntryType.Directory;

            this.ChangeHeader = string.Join(", ", GetChangeNamesFromFlags(info.Flags));

            this.Flags = info.Flags;
            this.IsNewItem = (info.Flags & SyncEntryChangedFlags.IsNew) != 0;
            this.IsUpdatedItem = (info.Flags & SyncEntryChangedFlags.IsUpdated) != 0;
            this.IsDeletedItem = (info.Flags & SyncEntryChangedFlags.Deleted) != 0;

            this.SizeOld = info.SizeOld;
            this.SizeNew = info.SizeNew;
            this.Sha1HashOld = info.Sha1HashOld;
            this.Sha1HashNew = info.Sha1HashNew;
            this.Md5HashOld = info.Md5HashOld;
            this.Md5HashNew = info.Md5HashNew;
            this.CreationDateTimeUtcOld = info.CreationDateTimeUtcOld;
            this.CreationDateTimeUtcNew = info.CreationDateTimeUtcNew;
            this.ModifiedDateTimeUtcOld = info.ModifiedDateTimeUtcOld;
            this.ModifiedDateTimeUtcNew = info.ModifiedDateTimeUtcNew;
            this.PathOld = info.PathOld;
            this.PathNew = info.PathNew;
        }

        public SyncEntryChangedFlags Flags { get; set; }

        public EntryUpdateInfoViewModel()
        {
            this.IsExpanded = true;
        }

        private ObservableCollection<EntryUpdateInfoViewModel> childEntries;

        public EntryUpdateInfoViewModel(SyncHistoryEntryData entry)
        {
            this.metadataChange = entry;
            var pathParts = entry.PathNew.Split('\\');

            this.Name = pathParts.Last();
            this.RelativePath = entry.PathNew;

            this.IsDirectory = entry.SyncEntry.Type == SyncEntryType.Directory;

            this.ChangeHeader = string.Join(", ", GetChangeNamesFromFlags(entry.Flags));

            this.Flags = entry.Flags;
            this.IsNewItem = (entry.Flags & SyncEntryChangedFlags.IsNew) != 0;
            this.IsUpdatedItem = (entry.Flags & SyncEntryChangedFlags.IsUpdated) != 0;
            this.IsDeletedItem = (entry.Flags & SyncEntryChangedFlags.Deleted) != 0;

            this.SizeOld = entry.SizeOld;
            this.SizeNew = entry.SizeNew;
            this.Sha1HashOld = entry.Sha1HashOld;
            this.Sha1HashNew = entry.Sha1HashNew;
            this.Md5HashOld = entry.Md5HashOld;
            this.Md5HashNew = entry.Md5HashNew;
            this.CreationDateTimeUtcOld = entry.CreationDateTimeUtcOld;
            this.CreationDateTimeUtcNew = entry.CreationDateTimeUtcNew;
            this.ModifiedDateTimeUtcOld = entry.ModifiedDateTimeUtcOld;
            this.ModifiedDateTimeUtcNew = entry.ModifiedDateTimeUtcNew;
            this.PathOld = entry.PathOld;
            this.PathNew = entry.PathNew;
        }

        private ISyncEntryMetadataChange metadataChange;

        public ObservableCollection<EntryUpdateInfoViewModel> ChildEntries => 
            this.childEntries ?? (this.childEntries = new ObservableCollection<EntryUpdateInfoViewModel>());

        private static IList<string> GetChangeNamesFromFlags(SyncEntryChangedFlags flags)
        {
            List<string> changes = new List<string>();

            if ((flags & SyncEntryChangedFlags.NewDirectory) == SyncEntryChangedFlags.NewDirectory)
            {
                changes.Add("New Directory");
            }

            if ((flags & SyncEntryChangedFlags.NewFile) == SyncEntryChangedFlags.NewFile)
            {
                changes.Add("New File");
            }

            if ((flags & SyncEntryChangedFlags.Sha1Hash) == SyncEntryChangedFlags.Sha1Hash)
            {
                changes.Add("SHA1 Hash Changed");
            }

            if ((flags & SyncEntryChangedFlags.Deleted) == SyncEntryChangedFlags.Deleted)
            {
                changes.Add("Deleted");
            }

            if ((flags & SyncEntryChangedFlags.Restored) == SyncEntryChangedFlags.Restored)
            {
                changes.Add("Restored");
            }

            if ((flags & SyncEntryChangedFlags.Renamed) == SyncEntryChangedFlags.Renamed)
            {
                changes.Add("Renamed");
            }

            if ((flags & SyncEntryChangedFlags.FileSize) == SyncEntryChangedFlags.FileSize)
            {
                changes.Add("File Size Changed");
            }

            if ((flags & SyncEntryChangedFlags.CreatedTimestamp) == SyncEntryChangedFlags.CreatedTimestamp)
            {
                changes.Add("Created Time");
            }

            if ((flags & SyncEntryChangedFlags.ModifiedTimestamp) == SyncEntryChangedFlags.ModifiedTimestamp)
            {
                changes.Add("Modified Time");
            }

            if ((flags & SyncEntryChangedFlags.Md5Hash) == SyncEntryChangedFlags.Md5Hash)
            {
                changes.Add("HD5 Hash Changed");
            }

            return changes;
        }
    }
}