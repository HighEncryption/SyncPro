namespace SyncPro.UI.Navigation
{
    using System.Diagnostics;
    using System.Windows.Input;

    using SyncPro.UI.Framework;
    using SyncPro.UI.Framework.MVVM;
    using SyncPro.UI.Navigation.ViewModels;
    using SyncPro.UI.ViewModels;

    public enum SyncRunFilesViewMode
    {
        Flat,
        Tree
    }

    public class SyncRunPanelViewModel : ViewModelBase
    {
        public SyncRelationshipViewModel Relationship { get; }

        public ICommand BeginAnalyzeCommand { get; }

        public ICommand BeginSyncCommand { get; }

        public SyncRunPanelViewModel(SyncRelationshipViewModel relationship)
        {
            this.Relationship = relationship;
            this.BeginAnalyzeCommand = new DelegatedCommand(this.BeginAnalyze, this.CanBeginAnalyze);
            this.BeginSyncCommand = new DelegatedCommand(this.BeginSync, this.CanBeginAnalyze);

            this.FileDisplayMode = SyncRunFilesViewMode.Flat;

            this.Relationship.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(this.Relationship.IsSyncActive))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            };
        }

        private void BeginSync(object obj)
        {
            // This will re-use the result from the analyze phase (if present) so that what is synced exactly 
            // matches what is seen in the results. This is important because we want to be sure that we dont
            // synchronize any changes that arent shown in the results page.
            this.Relationship.StartSyncRun(this.SyncRun.SyncRun.AnalyzeResult);
        }

        private bool CanBeginAnalyze(object obj)
        {
            return !this.Relationship.IsSyncActive;
        }

        private void BeginAnalyze(object obj)
        {
            this.SyncRun = this.Relationship.StartSyncRun(SyncTriggerType.Manual, true);
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool showAnalyzeControls;

        public bool ShowAnalyzeControls
        {
            get { return this.showAnalyzeControls; }
            set { this.SetProperty(ref this.showAnalyzeControls, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private SyncRunViewModel syncRun;

        public SyncRunViewModel SyncRun
        {
            get { return this.syncRun; }
            set { this.SetProperty(ref this.syncRun, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private EntryUpdateInfoViewModel selectedSyncEntry;

        public EntryUpdateInfoViewModel SelectedSyncEntry
        {
            get { return this.selectedSyncEntry; }
            set { this.SetProperty(ref this.selectedSyncEntry, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private SyncRunFilesViewMode fileDisplayMode;

        public SyncRunFilesViewMode FileDisplayMode
        {
            get { return this.fileDisplayMode; }
            set { this.SetProperty(ref this.fileDisplayMode, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool isAnalyzeOnlyMode;

        public bool IsAnalyzeOnlyMode
        {
            get { return this.isAnalyzeOnlyMode; }
            set { this.SetProperty(ref this.isAnalyzeOnlyMode, value); }
        }
    }
}