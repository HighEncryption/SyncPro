namespace SyncPro.UI.Navigation.ViewModels
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;

    using SyncPro.Data;
    using SyncPro.UI.Navigation.MenuCommands;
    using SyncPro.UI.ViewModels;

    public class FilesAndFoldersNodeViewModel : NavigationNodeViewModel, IFolderNodeViewModel
    {
        private readonly SyncRelationshipViewModel syncRelationship;

        private ObservableCollection<SyncEntryViewModel> syncEntries;

        public ObservableCollection<SyncEntryViewModel> SyncEntries
            => this.syncEntries ?? (this.syncEntries = new ObservableCollection<SyncEntryViewModel>());

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool noChildSyncEntries;

        public bool NoChildSyncEntries
        {
            get { return this.noChildSyncEntries; }
            set { this.SetProperty(ref this.noChildSyncEntries, value); }
        }

        private SyncEntryViewModel selectedChildEntry;

        public SyncEntryViewModel SelectedChildEntry
        {
            get { return this.selectedChildEntry; }
            set { this.SetProperty(ref this.selectedChildEntry, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private IList<SyncEntryViewModel> selectedChildEntries;

        public IList<SyncEntryViewModel> SelectedChildEntries
        {
            get { return this.selectedChildEntries; }
            set
            {
                if (this.SetProperty(ref this.selectedChildEntries, value))
                {
                    if (value != null && value.Count == 1)
                    {
                        this.SelectedChildEntry = value.First();
                    }
                    else
                    {
                        this.SelectedChildEntry = null;
                    }
                }
            }
        }

        public FilesAndFoldersNodeViewModel(NavigationNodeViewModel parent, SyncRelationshipViewModel syncRelationship) 
            : base(parent, syncRelationship, LazyLoadPlaceholderNodeViewModel.Instance)
        {
            this.syncRelationship = syncRelationship;
            this.Name = "Files & Folders";
            this.IconImageSource = "/SyncPro.UI;component/Resources/Graphics/folder_open_16.png";

            this.MenuCommands.Add(new RestoreItemMenuCommand(this));
        }

        protected override void LoadChildren()
        {
            Task.Factory.StartNew(() =>
            {
                using (var db = this.syncRelationship.GetDatabase())
                {
                    var rootEntry = db.Entries.First(e => e.ParentId == null);
                    var entries = db.Entries.Where(e => e.ParentId == rootEntry.Id).ToList();
                    App.DispatcherInvoke(() =>
                    {
                        foreach (SyncEntry entry in entries)
                        {

                            if (entry.Type == SyncEntryType.Directory)
                            {
                                var navigationNodeViewModel = new SyncFoldersNodeViewModel(this, this.syncRelationship,
                                    entry);
                                this.Children.Add(navigationNodeViewModel);
                                this.SyncEntries.Add(new SyncEntryViewModel(navigationNodeViewModel, entry, this.syncRelationship));
                            }
                            else
                            {
                                this.SyncEntries.Add(new SyncEntryViewModel(null, entry, this.syncRelationship));
                            }
                        }

                        this.NoChildSyncEntries = !this.SyncEntries.Any();
                    });
                }

                if (!this.Children.Any())
                {
                    App.DispatcherInvoke(() => { this.RaisePropertyChanged(nameof(this.Children)); });
                }
            });
        }
    }
}