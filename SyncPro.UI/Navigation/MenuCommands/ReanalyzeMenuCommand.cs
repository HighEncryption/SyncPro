namespace SyncPro.UI.Navigation.MenuCommands
{
    public class ReanalyzeMenuCommand : NavigationItemMenuCommand
    {
        private readonly SyncRunPanelViewModel syncRunPanel;

        public ReanalyzeMenuCommand(SyncRunPanelViewModel syncRunPanel)
            : base("RE-ANALYZE", "/SyncPro.UI;component/Resources/Graphics/select_invert_16.png")
        {
            this.syncRunPanel = syncRunPanel;
        }

        protected override bool CanInvokeCommand(object obj)
        {
            return this.syncRunPanel.BeginAnalyzeCommand.CanExecute(obj);
        }

        protected override void InvokeCommand(object obj)
        {
            this.syncRunPanel.BeginAnalyzeCommand.Execute(obj);
        }
    }
}