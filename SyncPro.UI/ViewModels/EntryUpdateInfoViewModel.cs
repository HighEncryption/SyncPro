namespace SyncPro.UI.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Linq;

    using SyncPro.Adapters;
    using SyncPro.Data;
    using SyncPro.Runtime;
    using SyncPro.UI.Framework;

    /// <summary>
    /// ViewModel wrapper for the <see cref="EntryUpdateInfo"/> class, which contains 
    /// information about the changes being made to an entry
    /// </summary>
    public class EntryUpdateInfoViewModel : ViewModelBase
    {
        public EntryUpdateInfo Info { get; }

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

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string relativePath;

        public string RelativePath
        {
            get { return this.relativePath; }
            set { this.SetProperty(ref this.relativePath, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool isDirectory;

        public bool IsDirectory
        {
            get { return this.isDirectory; }
            set { this.SetProperty(ref this.isDirectory, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string changeHeader;

        public string ChangeHeader
        {
            get { return this.changeHeader; }
            set { this.SetProperty(ref this.changeHeader, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool noChange;

        public bool NoChange
        {
            get { return this.noChange; }
            set { this.SetProperty(ref this.noChange, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool isNewItem;

        public bool IsNewItem
        {
            get { return this.isNewItem; }
            set { this.SetProperty(ref this.isNewItem, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool isUpdatedItem;

        public bool IsUpdatedItem
        {
            get { return this.isUpdatedItem; }
            set { this.SetProperty(ref this.isUpdatedItem, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool isDeletedItem;

        public bool IsDeletedItem
        {
            get { return this.isDeletedItem; }
            set { this.SetProperty(ref this.isDeletedItem, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private DateTime lastModified;

        public DateTime LastModified
        {
            get { return this.lastModified; }
            set { this.SetProperty(ref this.lastModified, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private long size;

        public long Size
        {
            get { return this.size; }
            set { this.SetProperty(ref this.size, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool isExpanded;

        public bool IsExpanded
        {
            get { return this.isExpanded; }
            set { this.SetProperty(ref this.isExpanded, value); }
        }

        public EntryUpdateInfoViewModel(EntryUpdateInfo info)
        {
            this.Info = info;

            this.Name = info.Entry.Name;
            this.RelativePath = info.RelativePath;

            this.IsDirectory = info.Entry.Type == SyncEntryType.Directory;

            this.ChangeHeader = string.Join(", ", GetChangeNamesFromFlags(info.Flags));

            this.IsNewItem = (info.Flags & SyncEntryChangedFlags.IsNew) != 0;
            this.IsUpdatedItem = (info.Flags & SyncEntryChangedFlags.IsUpdated) != 0;
            this.IsDeletedItem = (info.Flags & SyncEntryChangedFlags.Deleted) != 0;

            this.LastModified = info.Entry.ModifiedDateTimeUtc;
            this.Size = info.Entry.Size;
        }

        public EntryUpdateInfoViewModel()
        {
            this.IsExpanded = true;
        }

        private ObservableCollection<EntryUpdateInfoViewModel> childEntries;

        public EntryUpdateInfoViewModel(SyncHistoryEntryData entry)
        {
            var pathParts = entry.PathNew.Split('\\');

            this.Name = pathParts.Last();
            this.RelativePath = entry.PathNew;

            this.IsDirectory = entry.SyncEntry.Type == SyncEntryType.Directory;

            this.ChangeHeader = string.Join(", ", GetChangeNamesFromFlags(entry.Flags));

            this.IsNewItem = (entry.Flags & SyncEntryChangedFlags.IsNew) != 0;
            this.IsUpdatedItem = (entry.Flags & SyncEntryChangedFlags.IsUpdated) != 0;
            this.IsDeletedItem = (entry.Flags & SyncEntryChangedFlags.Deleted) != 0;

            this.LastModified = entry.Timestamp;
            this.Size = entry.SizeNew;
        }

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

            return changes;
        }
    }
}