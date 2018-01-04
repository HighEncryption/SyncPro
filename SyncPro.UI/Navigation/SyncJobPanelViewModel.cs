namespace SyncPro.UI.Navigation
{
    using System.Diagnostics;
    using System.Windows.Input;

    using SyncPro.UI.Framework;
    using SyncPro.UI.Framework.MVVM;
    using SyncPro.UI.ViewModels;

    public enum SyncJobFilesViewMode
    {
        Flat,
        Tree
    }

    public class SyncJobPanelViewModel : ViewModelBase
    {
        public SyncRelationshipViewModel Relationship { get; }

        public ICommand BeginAnalyzeCommand { get; }

        public ICommand BeginSyncCommand { get; }

        public SyncJobPanelViewModel(SyncRelationshipViewModel relationship)
        {
            this.Relationship = relationship;
            this.BeginAnalyzeCommand = new DelegatedCommand(this.BeginAnalyze, this.CanBeginAnalyze);
            this.BeginSyncCommand = new DelegatedCommand(this.BeginSync, this.CanBeginAnalyze);

            this.FileDisplayMode = SyncJobFilesViewMode.Flat;

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
            this.Relationship.StartSyncJob(this.SyncJob.SyncJob.AnalyzeResult);
        }

        private bool CanBeginAnalyze(object obj)
        {
            return !this.Relationship.IsSyncActive;
        }

        private void BeginAnalyze(object obj)
        {
            this.SyncJob = this.Relationship.StartSyncJob(SyncTriggerType.Manual, true);
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool showAnalyzeControls;

        public bool ShowAnalyzeControls
        {
            get { return this.showAnalyzeControls; }
            set { this.SetProperty(ref this.showAnalyzeControls, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private SyncJobViewModel syncJob;

        public SyncJobViewModel SyncJob
        {
            get { return this.syncJob; }
            set { this.SetProperty(ref this.syncJob, value); }
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

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool isAnalyzeOnlyMode;

        public bool IsAnalyzeOnlyMode
        {
            get { return this.isAnalyzeOnlyMode; }
            set { this.SetProperty(ref this.isAnalyzeOnlyMode, value); }
        }
    }
}