using SyncPro.Runtime;

namespace SyncPro.UI.Navigation.MenuCommands
{
    public class CancelOperationMenuCommand : NavigationItemMenuCommand
    {
        private readonly SyncJobPanelViewModel syncJobPanel;

        public CancelOperationMenuCommand(SyncJobPanelViewModel syncJobPanel)
            : base("CANCEL", "/SyncPro.UI;component/Resources/Graphics/stop_16.png")
        {
            this.syncJobPanel = syncJobPanel;
        }

        protected override bool CanInvokeCommand(object obj)
        {
            return this.syncJobPanel.Relationship.State == SyncRelationshipState.Running;
        }

        protected override void InvokeCommand(object obj)
        {
            this.syncJobPanel.SyncJob.CancelSyncJobCommand.Execute(null);
        }
    }
}