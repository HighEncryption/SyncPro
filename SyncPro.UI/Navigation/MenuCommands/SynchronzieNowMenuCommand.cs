namespace SyncPro.UI.Navigation.MenuCommands
{
    public class SynchronzieNowMenuCommand : NavigationItemMenuCommand
    {
        private readonly AnalyzeJobPanelViewModel analyzeJobPanel;

        public SynchronzieNowMenuCommand(AnalyzeJobPanelViewModel analyzeJobPanel)
            : base("SYNCHRONIZE NOW", "/SyncPro.UI;component/Resources/Graphics/refresh_update_16.png")
        {
            this.analyzeJobPanel = analyzeJobPanel;
        }

        protected override bool CanInvokeCommand(object obj)
        {
            return this.analyzeJobPanel.BeginSyncCommand.CanExecute(obj);
        }

        protected override void InvokeCommand(object obj)
        {
            this.analyzeJobPanel.BeginSyncCommand.Execute(obj);
        }
    }
}