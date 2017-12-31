namespace SyncPro.UI.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows.Media;

    using SyncPro.Adapters;
    using SyncPro.Data;
    using SyncPro.Runtime;
    using SyncPro.UI.Framework;
    using SyncPro.UI.Navigation.ViewModels;
    using SyncPro.UI.Utility;

    /// <summary>
    /// ViewModel wrapper for the <see cref="EntryUpdateInfo"/> class, which contains 
    /// information about the changes being made to an entry.
    /// </summary>
    public class EntryUpdateInfoViewModel : ViewModelBase, ISyncEntryMetadataChange
    {
        private readonly SyncRelationshipViewModel syncRelationship;

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

        public long SourceSizeOld { get; set; }
        public long SourceSizeNew { get; set; }
        public long DestinationSizeOld { get; set; }
        public long DestinationSizeNew { get; set; }
        public byte[] SourceSha1HashOld { get; set; }
        public byte[] SourceSha1HashNew { get; set; }
        public byte[] DestinationSha1HashOld { get; set; }
        public byte[] DestinationSha1HashNew { get; set; }
        public byte[] SourceMd5HashOld { get; set; }
        public byte[] SourceMd5HashNew { get; set; }
        public byte[] DestinationMd5HashOld { get; set; }
        public byte[] DestinationMd5HashNew { get; set; }

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

                this.BeginLoadSyncRunReferences();

                return this.largeIcon;
            }
        }

        public EntryUpdateInfoViewModel(EntryUpdateInfo info, SyncRelationshipViewModel syncRelationship)
        {
            this.syncRelationship = syncRelationship;
            this.Name = info.Entry.Name;
            this.RelativePath = info.RelativePath;
            this.syncEntryId = info.Entry.Id;

            this.IsDirectory = info.Entry.Type == SyncEntryType.Directory;

            this.ChangeHeader = string.Join(", ", GetChangeNamesFromFlags(info.Flags));

            this.Flags = info.Flags;
            this.IsNewItem = (info.Flags & SyncEntryChangedFlags.IsNew) != 0;
            this.IsUpdatedItem = (info.Flags & SyncEntryChangedFlags.IsUpdated) != 0;
            this.IsDeletedItem = (info.Flags & SyncEntryChangedFlags.Deleted) != 0;

            this.SourceSizeOld = info.SourceSizeOld;
            this.DestinationSizeOld = info.DestinationSizeOld;
            this.SourceSizeNew = info.SourceSizeNew;
            this.DestinationSizeNew = info.DestinationSizeNew;
            this.SourceSha1HashOld = info.SourceSha1HashOld;
            this.DestinationSha1HashOld = info.DestinationSha1HashOld;
            this.SourceSha1HashNew = info.SourceSha1HashNew;
            this.DestinationSha1HashNew = info.DestinationSha1HashNew;
            this.SourceMd5HashOld = info.SourceMd5HashOld;
            this.DestinationMd5HashOld = info.DestinationMd5HashOld;
            this.SourceMd5HashNew = info.SourceMd5HashNew;
            this.DestinationMd5HashNew = info.DestinationMd5HashNew;
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

        public EntryUpdateInfoViewModel(SyncHistoryEntryData entry, SyncRelationshipViewModel syncRelationship)
        {
            this.syncRelationship = syncRelationship;
            var pathParts = entry.PathNew.Split('\\');
            this.syncEntryId = entry.SyncEntryId;

            this.Name = pathParts.Last();
            this.RelativePath = entry.PathNew;

            this.IsDirectory = entry.SyncEntry.Type == SyncEntryType.Directory;

            this.ChangeHeader = string.Join(", ", GetChangeNamesFromFlags(entry.Flags));

            this.Flags = entry.Flags;
            this.IsNewItem = (entry.Flags & SyncEntryChangedFlags.IsNew) != 0;
            this.IsUpdatedItem = (entry.Flags & SyncEntryChangedFlags.IsUpdated) != 0;
            this.IsDeletedItem = (entry.Flags & SyncEntryChangedFlags.Deleted) != 0;

            this.SourceSizeOld = entry.SourceSizeOld;
            this.DestinationSizeOld = entry.DestinationSizeOld;
            this.SourceSizeNew = entry.SourceSizeNew;
            this.DestinationSizeNew = entry.DestinationSizeNew;
            this.SourceSha1HashOld = entry.SourceSha1HashOld;
            this.DestinationSha1HashOld = entry.DestinationSha1HashOld;
            this.SourceSha1HashNew = entry.SourceSha1HashNew;
            this.DestinationSha1HashNew = entry.DestinationSha1HashNew;
            this.SourceMd5HashOld = entry.SourceMd5HashOld;
            this.DestinationMd5HashOld = entry.DestinationMd5HashOld;
            this.SourceMd5HashNew = entry.SourceMd5HashNew;
            this.DestinationMd5HashNew = entry.DestinationMd5HashNew;
            this.CreationDateTimeUtcOld = entry.CreationDateTimeUtcOld;
            this.CreationDateTimeUtcNew = entry.CreationDateTimeUtcNew;
            this.ModifiedDateTimeUtcOld = entry.ModifiedDateTimeUtcOld;
            this.ModifiedDateTimeUtcNew = entry.ModifiedDateTimeUtcNew;
            this.PathOld = entry.PathOld;
            this.PathNew = entry.PathNew;
        }

        public ObservableCollection<EntryUpdateInfoViewModel> ChildEntries => 
            this.childEntries ?? (this.childEntries = new ObservableCollection<EntryUpdateInfoViewModel>());

        public bool ShowSizeOld => (this.Flags & SyncEntryChangedFlags.FileSize) != 0 && !this.IsDirectory;

        private ObservableCollection<SyncRunReferenceViewModel> syncRunReferences;

        public ObservableCollection<SyncRunReferenceViewModel> SyncRunReferences
            => this.syncRunReferences ?? (this.syncRunReferences = new ObservableCollection<SyncRunReferenceViewModel>());

        private volatile object loadLock = new object();

        private bool isLoadingStarted = false;
        private readonly long syncEntryId;

        private void BeginLoadSyncRunReferences()
        {
            if (!this.isLoadingStarted)
            {
                lock (this.loadLock)
                {
                    if (!this.isLoadingStarted)
                    {
                        this.isLoadingStarted = true;
                        Task.Factory.StartNew(this.BeginLoadSyncRunReferencesInternal);
                    }
                }
            }
        }

        private void BeginLoadSyncRunReferencesInternal()
        {
            using (var db = this.syncRelationship.GetDatabase())
            {
                // Get the list of sync history entries for this file (slow)
                List<SyncHistoryEntryData> historyEntries =
                    db.HistoryEntries.Where(e => e.SyncEntryId == this.syncEntryId).ToList();
                var idList = historyEntries.Select(e => e.SyncHistoryId).ToList();

                // Get the sync runs for those entries (fast)
                IQueryable<SyncHistoryData> matches = db.History.Where(
                    EntityFrameworkExtensions.BuildOrExpression<SyncHistoryData, int>(p => p.Id, idList));

                foreach (SyncHistoryData historyData in matches)
                {
                    App.DispatcherInvoke(() => this.SyncRunReferences.Add(new SyncRunReferenceViewModel(
                        historyData.Start.ToString("g"), historyData.Id)));
                }
            }
        }

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