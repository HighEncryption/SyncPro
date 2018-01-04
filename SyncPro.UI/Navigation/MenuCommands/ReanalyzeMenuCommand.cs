namespace SyncPro.UI.Navigation.MenuCommands
{
    public class ReanalyzeMenuCommand : NavigationItemMenuCommand
    {
        private readonly SyncJobPanelViewModel syncJobPanel;

        public ReanalyzeMenuCommand(SyncJobPanelViewModel syncJobPanel)
            : base("RE-ANALYZE", "/SyncPro.UI;component/Resources/Graphics/select_invert_16.png")
        {
            this.syncJobPanel = syncJobPanel;
        }

        protected override bool CanInvokeCommand(object obj)
        {
            return this.syncJobPanel.BeginAnalyzeCommand.CanExecute(obj);
        }

        protected override void InvokeCommand(object obj)
        {
            this.syncJobPanel.BeginAnalyzeCommand.Execute(obj);
        }
    }
}