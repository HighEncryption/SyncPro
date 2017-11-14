namespace SyncPro.UI.Navigation.ViewModels
{
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;

    using SyncPro.Data;
    using SyncPro.UI.ViewModels;

    public class FilesAndFoldersNodeViewModel : NavigationNodeViewModel
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

        public FilesAndFoldersNodeViewModel(NavigationNodeViewModel parent, SyncRelationshipViewModel syncRelationship) 
            : base(parent, syncRelationship, LazyLoadPlaceholderNodeViewModel.Instance)
        {
            this.syncRelationship = syncRelationship;
            this.Name = "Files & Folders";
            this.IconImageSource = "/SyncPro.UI;component/Resources/Graphics/folder_open_16.png";
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