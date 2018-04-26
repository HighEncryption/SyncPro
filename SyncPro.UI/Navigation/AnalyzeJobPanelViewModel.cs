namespace SyncPro.UI.Navigation
{
    using System.Diagnostics;
    using System.Windows.Input;

    using SyncPro.Runtime;
    using SyncPro.UI.Framework;
    using SyncPro.UI.Framework.MVVM;
    using SyncPro.UI.ViewModels;

    public class AnalyzeJobPanelViewModel : ViewModelBase, ICancelJobPanelViewModel
    {
        public SyncRelationshipViewModel Relationship { get; }

        public ICommand CancelJobCommand { get; }

        public ICommand BeginAnalyzeCommand { get; }

        public ICommand BeginSyncCommand { get; }

        private AnalyzeJob promisedJob;

        public AnalyzeJobPanelViewModel(SyncRelationshipViewModel relationship)
        {
            this.Relationship = relationship;
            this.BeginAnalyzeCommand = new DelegatedCommand(this.BeginAnalyze, this.CanBeginAnalyze);
            this.BeginSyncCommand = new DelegatedCommand(this.BeginSync, this.CanBeginAnalyze);
            this.CancelJobCommand = new DelegatedCommand(this.CancelJob, this.CanCancelJob);

            this.FileDisplayMode = SyncJobFilesViewMode.Flat;

            this.Relationship.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(this.Relationship.ActiveJob))
                {
                    var activeJobViewModel = this.Relationship.ActiveJob as AnalyzeJobViewModel;
                    if (activeJobViewModel != null && activeJobViewModel.Job == this.promisedJob)
                    {
                        this.AnalyzeJobViewModel = activeJobViewModel;
                    }

                    App.DispatcherInvoke(CommandManager.InvalidateRequerySuggested);
                }
            };
        }

        private bool CanCancelJob(object obj)
        {
            return this.AnalyzeJobViewModel != null && this.AnalyzeJobViewModel.CancelJobCommand.CanExecute(obj);
        }

        private void CancelJob(object obj)
        {
            this.AnalyzeJobViewModel.CancelJobCommand.Execute(obj);
        }

        public void Closing()
        {
            this.Relationship.GetSyncRelationship().ActiveAnalyzeJob = null;
        }

        private void BeginSync(object obj)
        {
            // This will re-use the result from the analyze phase (if present) so that what is synced exactly 
            // matches what is seen in the results. This is important because we want to be sure that we dont
            // synchronize any changes that arent shown in the results page.
            this.Relationship.StartSyncJob(this.AnalyzeJobViewModel.AnalyzeJob.AnalyzeResult);
        }

        private bool CanBeginAnalyze(object obj)
        {
            return this.Relationship.ActiveJob == null;
        }

        private void BeginAnalyze(object obj)
        {
            // We are asking the relationship viewmodel to create a new analyze job. The viewmodel for that job is
            // created asynchronously, so we can't have it passed back to us here. Instead, we are passed the actual
            // job object, which we will treat as a 'promise' of a viewmodel to be created later. When the viewmodel
            // is eventually created, it will be assigned to the ActiveJob property on the relationship ifself. We
            // can then wait for a callback that the ActiveJob property has changed, and set our local viewmodel
            // property to be that viewmodel if the underlying job matches the job we were promised here.
            this.promisedJob = this.Relationship.StartAnalyzeJob(false);

            this.promisedJob.Start();
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool showAnalyzeControls;

        public bool ShowAnalyzeControls
        {
            get { return this.showAnalyzeControls; }
            set { this.SetProperty(ref this.showAnalyzeControls, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private AnalyzeJobViewModel analyzeJobViewModel;

        public AnalyzeJobViewModel AnalyzeJobViewModel
        {
            get { return this.analyzeJobViewModel; }
            set { this.SetProperty(ref this.analyzeJobViewModel, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private EntryUpdateInfoViewModel selectedSyncEntry;

        public EntryUpdateInfoViewModel SelectedSyncEntry
        {
            get { return this.selectedSyncEntry; }
            set
            {
                if (this.SetProperty(ref this.selectedSyncEntry, value))
                {
                    this.selectedSyncEntry.LoadThumbnails();
                }
            }
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