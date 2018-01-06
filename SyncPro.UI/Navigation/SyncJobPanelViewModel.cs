namespace SyncPro.UI.Navigation
{
    using System.Diagnostics;
    using System.Windows.Input;

    using SyncPro.UI.Framework;
    using SyncPro.UI.ViewModels;

    public enum SyncJobFilesViewMode
    {
        Flat,
        Tree
    }

    public interface ICancelJobPanelViewModel
    {
        ICommand CancelJobCommand { get; }
    }

    public class SyncJobPanelViewModel : ViewModelBase, ICancelJobPanelViewModel
    {
        public SyncRelationshipViewModel Relationship { get; }

        public ICommand CancelJobCommand => this.SyncJobViewModel.CancelJobCommand;

        public SyncJobPanelViewModel(SyncRelationshipViewModel relationship)
        {
            this.Relationship = relationship;

            this.FileDisplayMode = SyncJobFilesViewMode.Flat;

            this.Relationship.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(this.Relationship.ActiveJob))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            };
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private SyncJobViewModel syncJobViewModel;

        public SyncJobViewModel SyncJobViewModel
        {
            get { return this.syncJobViewModel; }
            set { this.SetProperty(ref this.syncJobViewModel, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private EntryUpdateInfoViewModel selectedSyncEntry;

        public EntryUpdateInfoViewModel SelectedSyncEntry
        {
            get { return this.selectedSyncEntry; }
            set { this.SetProperty(ref this.selectedSyncEntry, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private SyncJobFilesViewMode fileDisplayMode;

        public SyncJobFilesViewMode FileDisplayMode
        {
            get { return this.fileDisplayMode; }
            set { this.SetProperty(ref this.fileDisplayMode, value); }
        }
    }
}