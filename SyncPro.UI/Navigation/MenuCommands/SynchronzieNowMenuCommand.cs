namespace SyncPro.UI.Navigation.MenuCommands
{
    public class SynchronzieNowMenuCommand : NavigationItemMenuCommand
    {
        private readonly SyncRunPanelViewModel syncRunPanel;

        public SynchronzieNowMenuCommand(SyncRunPanelViewModel syncRunPanel)
            : base("SYNCHRONIZE NOW", "/SyncPro.UI;component/Resources/Graphics/refresh_update_16.png")
        {
            this.syncRunPanel = syncRunPanel;
        }

        protected override bool CanInvokeCommand(object obj)
        {
            return this.syncRunPanel.BeginSyncCommand.CanExecute(obj);
        }

        protected override void InvokeCommand(object obj)
        {
            this.syncRunPanel.BeginSyncCommand.Execute(obj);
        }
    }
}