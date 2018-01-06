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
                if (args.PropertyName == nameof(this.relationship.ActiveJob))
                {
                    this.UpdateState();
                }
            };

            this.UpdateState();
        }

        private void UpdateState()
        {
            if (this.relationship.ActiveJob != null)
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
            var activeSyncJob = this.relationship.ActiveJob;
            if (this.isCancelMode && activeSyncJob != null)
            {
                return activeSyncJob.CancelJobCommand.CanExecute(obj);
            }

            return true;
        }

        protected override void InvokeCommand(object obj)
        {
            var activeSyncJob = this.relationship.ActiveJob;

            if (this.isCancelMode && activeSyncJob != null)
            {
                activeSyncJob.CancelJobCommand.Execute(obj);
            }
            else
            {
                this.relationship.SyncNowCommand.Execute(null);
            }
        }
    }
}