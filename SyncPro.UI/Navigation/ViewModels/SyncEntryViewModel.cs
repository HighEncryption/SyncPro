namespace SyncPro.UI.Navigation.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading.Tasks;
    using System.Windows.Input;
    using System.Windows.Media;

    using SyncPro.Data;
    using SyncPro.UI.Framework;
    using SyncPro.UI.Framework.MVVM;
    using SyncPro.UI.Utility;
    using SyncPro.UI.ViewModels;

    public class SyncRunReferenceViewModel : ViewModelBase
    {
        public string DisplayName { get; }

        public int Id { get; }

        public SyncRunReferenceViewModel(string displayName, int runId)
        {
            this.DisplayName = displayName;
            this.Id = runId;
        }
    }

    public class SyncEntryViewModel : ViewModelBase
    {
        private readonly NavigationNodeViewModel navigationNodeViewModel;
        private readonly SyncRelationshipViewModel syncRelationship;
        public SyncEntry SyncEntry { get; }

        public ICommand SelectItemCommand { get; }

        private ObservableCollection<SyncRunReferenceViewModel> syncRunReferences;

        public ObservableCollection<SyncRunReferenceViewModel> SyncRunReferences
            => this.syncRunReferences ?? (this.syncRunReferences = new ObservableCollection<SyncRunReferenceViewModel>());

        public SyncEntryViewModel(NavigationNodeViewModel navigationNodeViewModel, SyncEntry syncEntry, SyncRelationshipViewModel syncRelationship)
        {
            this.navigationNodeViewModel = navigationNodeViewModel;
            this.syncRelationship = syncRelationship;
            this.SyncEntry = syncEntry;

            this.Name = syncEntry.Name;
            this.LastModified = syncEntry.EntryLastUpdatedDateTimeUtc;
            this.Size = Convert.ToUInt64(syncEntry.SourceSize);
            this.IsDirectory = syncEntry.Type == SyncEntryType.Directory;
            this.SelectItemCommand = new DelegatedCommand(o => this.SelectItem());

            if (this.IsDirectory)
            {
                var fileInfo = FileInfoCache.GetFolderInfo();
                this.IconImageSource = fileInfo.SmallIcon;
                this.TypeName = "Folder";
                return;
            }

            int lastIndex2 = this.Name.LastIndexOf(".", StringComparison.Ordinal);
            FileInfo fileInfo2 = FileInfoCache.GetFileInfo(this.Name.Substring(lastIndex2).ToLowerInvariant());

            this.IconImageSource = fileInfo2.SmallIcon;
            this.TypeName = fileInfo2.TypeName;
        }

        private void SelectItem()
        {
            if (this.IsDirectory)
            {
                this.navigationNodeViewModel.IsSelected = true;
            }
        }

        private bool isSelected;

        public bool IsSelected
        {
            get { return this.isSelected; }
            set
            {
                if (this.SetProperty(ref this.isSelected, value) && value)
                {
                    this.BeginLoadSyncRunReferences();
                }
            }
        }

        private volatile object loadLock = new object();

        private bool isLoadingStarted = false;

        private void BeginLoadSyncRunReferences()
        {
            if (!this.isLoadingStarted)
            {
                lock (this.loadLock)
                {
                    if (!this.isLoadingStarted)
                    {
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
                    db.HistoryEntries.Where(e => e.SyncEntryId == this.SyncEntry.Id).ToList();
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

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string name;

        public string Name
        {
            get { return this.name; }
            set { this.SetProperty(ref this.name, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private ImageSource iconImageSource;

        public ImageSource IconImageSource
        {
            get { return this.iconImageSource; }
            set { this.SetProperty(ref this.iconImageSource, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private DateTime lastModified;

        public DateTime LastModified
        {
            get { return this.lastModified; }
            set { this.SetProperty(ref this.lastModified, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string typeName;

        public string TypeName
        {
            get { return this.typeName; }
            set { this.SetProperty(ref this.typeName, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private ulong size;

        public ulong Size
        {
            get { return this.size; }
            set { this.SetProperty(ref this.size, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool isDirectory;

        public bool IsDirectory
        {
            get { return this.isDirectory; }
            set { this.SetProperty(ref this.isDirectory, value); }
        }

        private ObservableCollection<SyncEntryAdapterDataViewModel> sourceAdapters;

        public ObservableCollection<SyncEntryAdapterDataViewModel> SourceAdapters
            => this.sourceAdapters ?? (this.sourceAdapters = new ObservableCollection<SyncEntryAdapterDataViewModel>());

        private ObservableCollection<SyncEntryAdapterDataViewModel> destinationAdapters;

        public ObservableCollection<SyncEntryAdapterDataViewModel> DestinationAdapters
            => this.destinationAdapters ?? (this.destinationAdapters = new ObservableCollection<SyncEntryAdapterDataViewModel>());
    }

    public class SyncEntryAdapterDataViewModel : ViewModelBase
    {
    }

    public static class EntityFrameworkExtensions
    {
        public static Expression<Func<TElement, bool>> BuildOrExpression<TElement, TValue>(
        Expression<Func<TElement, TValue>> valueSelector,
        IList<TValue> values)
        {
            if (null == valueSelector)
            {
                throw new ArgumentNullException(nameof(valueSelector));
            }

            if (null == values)
            {
                throw new ArgumentNullException(nameof(values));
            }

            ParameterExpression p = valueSelector.Parameters.Single();

            if (!values.Any())
            {
                return e => false;
            }

            IEnumerable<Expression> equals = values.Select(value =>
                (Expression)Expression.Equal(
                     valueSelector.Body,
                     Expression.Constant(
                         value,
                         typeof(TValue)
                     )
                )
            );

            Expression body = equals.Aggregate(Expression.Or);

            return Expression.Lambda<Func<TElement, bool>>(body, p);
        }

    }
}