using SyncPro.Runtime;

namespace SyncPro.UI.Navigation.MenuCommands
{
    public class CancelOperationMenuCommand : NavigationItemMenuCommand
    {
        private readonly SyncRunPanelViewModel syncRunPanel;

        public CancelOperationMenuCommand(SyncRunPanelViewModel syncRunPanel)
            : base("CANCEL", "/SyncPro.UI;component/Resources/Graphics/stop_16.png")
        {
            this.syncRunPanel = syncRunPanel;
        }

        protected override bool CanInvokeCommand(object obj)
        {
            return this.syncRunPanel.Relationship.State == SyncRelationshipState.Running;
        }

        protected override void InvokeCommand(object obj)
        {
            this.syncRunPanel.SyncRun.CancelSyncRunCommand.Execute(null);
        }
    }
}