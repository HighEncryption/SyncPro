namespace SyncPro.UI.Navigation.MenuCommands
{
    public class SynchronzieNowMenuCommand : NavigationItemMenuCommand
    {
        private readonly SyncJobPanelViewModel syncJobPanel;

        public SynchronzieNowMenuCommand(SyncJobPanelViewModel syncJobPanel)
            : base("SYNCHRONIZE NOW", "/SyncPro.UI;component/Resources/Graphics/refresh_update_16.png")
        {
            this.syncJobPanel = syncJobPanel;
        }

        protected override bool CanInvokeCommand(object obj)
        {
            return this.syncJobPanel.BeginSyncCommand.CanExecute(obj);
        }

        protected override void InvokeCommand(object obj)
        {
            this.syncJobPanel.BeginSyncCommand.Execute(obj);
        }
    }
}