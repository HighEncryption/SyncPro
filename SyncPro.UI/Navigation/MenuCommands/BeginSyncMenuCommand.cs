namespace SyncPro.UI.Navigation.MenuCommands
{
    using SyncPro.UI.ViewModels;

    public class BeginSyncMenuCommand : NavigationItemMenuCommand
    {
        private readonly SyncRelationshipViewModel relationship;
        private bool isCancelMode;

        public BeginSyncMenuCommand(SyncRelationshipViewModel relationship)
            : base("SYNCHRONIZE", "/SyncPro.UI;component/Resources/Graphics/refresh_update_16.png")
        {
            this.relationship = relationship;

            this.relationship.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == "IsSyncActive")
                {
                    this.UpdateState();
                }
            };

            this.UpdateState();
        }

        private void UpdateState()
        {
            if (this.relationship.IsSyncActive)
            {
                this.Header = "CANCEL";
                this.IconImageSource = "/SyncPro.UI;component/Resources/Graphics/stop_16.png";
                this.isCancelMode = true;
            }
            else
            {
                this.Header = "SYNCHRONIZE";
                this.IconImageSource = "/SyncPro.UI;component/Resources/Graphics/refresh_update_16.png";
                this.isCancelMode = false;
            }
        }

        protected override bool CanInvokeCommand(object obj)
        {
            var activeSyncJob = this.relationship.ActiveSyncJob;
            if (this.isCancelMode && activeSyncJob != null)
            {
                return activeSyncJob.CancelSyncJobCommand.CanExecute(obj);
            }

            return true;
        }

        protected override void InvokeCommand(object obj)
        {
            var activeSyncJob = this.relationship.ActiveSyncJob;

            if (this.isCancelMode && activeSyncJob != null)
            {
                activeSyncJob.CancelSyncJobCommand.Execute(obj);
            }
            else
            {
                this.relationship.SyncNowCommand.Execute(null);
            }
        }
    }
}