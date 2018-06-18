namespace SyncPro.UI.ViewModels
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Windows.Input;

    using SyncPro.Runtime;
    using SyncPro.UI.Framework;
    using SyncPro.UI.Framework.MVVM;
    using SyncPro.UI.Navigation;
    using SyncPro.UI.Navigation.ViewModels;

    public class JobViewModel : ViewModelBase
    {
        public ICommand ShowJobCommand { get; }

        public ICommand CancelJobCommand { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private DateTime startTime;

        public DateTime StartTime
        {
            get { return this.startTime; }
            set { this.SetProperty(ref this.startTime, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private DateTime endTime;

        public DateTime EndTime
        {
            get => this.endTime;
            set
            {
                if (this.SetProperty(ref this.endTime, value))
                {
                    this.RaisePropertyChanged(nameof(this.IsFinished));
                }
            }
        }

        public bool IsFinished => this.EndTime != DateTime.MinValue;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool isProgressIndeterminate;

        public bool IsProgressIndeterminate
        {
            get { return this.isProgressIndeterminate; }
            set { this.SetProperty(ref this.isProgressIndeterminate, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private double progressValue;

        public double ProgressValue
        {
            get { return this.progressValue; }
            set { this.SetProperty(ref this.progressValue, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private ProgressState progressState;

        public ProgressState ProgressState
        {
            get { return this.progressState; }
            set { this.SetProperty(ref this.progressState, value); }
        }

        protected JobViewModel(JobBase job, SyncRelationshipViewModel relationship)
        {
            this.Job = job;
            this.SyncRelationship = relationship;

            this.ShowJobCommand = new DelegatedCommand(o => this.ShowJob());
            this.CancelJobCommand = new DelegatedCommand(o => this.CancelJob(), o => this.CanCancelJob());
        }

        public JobBase Job { get; }

        public SyncRelationshipViewModel SyncRelationship { get; }

        public void ShowJob()
        {
            // Find the navigation tree item for this relationship
            SyncRelationshipNodeViewModel relationshipNavItem =
                App.Current.MainWindowsViewModel.NavigationItems.OfType<SyncRelationshipNodeViewModel>().FirstOrDefault(
                    n => n.Item == this.SyncRelationship);

            Debug.Assert(relationshipNavItem != null, "relationshipNavItem != null");

            SyncHistoryNodeViewModel syncHistoryNode =
                relationshipNavItem.Children.OfType<SyncHistoryNodeViewModel>().First();

            // Check if a Sync History item is already present under this relationship for this history
            foreach (SyncJobNodeViewModel syncJobNodeViewModel in syncHistoryNode.Children.OfType<SyncJobNodeViewModel>())
            {
                var panelViewModel = syncJobNodeViewModel.Item as SyncJobPanelViewModel;
                if (panelViewModel != null && panelViewModel.SyncJobViewModel == this)
                {
                    syncJobNodeViewModel.IsSelected = true;
                    return;
                }
            }
        }

        private bool CanCancelJob()
        {
            return this.Job.HasStarted && !this.Job.HasFinished;
        }

        private void CancelJob()
        {
            if (this.Job.HasStarted)
            {
                this.Job.Cancel();
            }
        }
    }
}