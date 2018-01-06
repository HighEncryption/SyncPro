namespace SyncPro.UI.Navigation.MenuCommands
{
    public class ReanalyzeMenuCommand : NavigationItemMenuCommand
    {
        private readonly AnalyzeJobPanelViewModel analyzeJobPanel;

        public ReanalyzeMenuCommand(AnalyzeJobPanelViewModel analyzeJobPanel)
            : base("RE-ANALYZE", "/SyncPro.UI;component/Resources/Graphics/select_invert_16.png")
        {
            this.analyzeJobPanel = analyzeJobPanel;
        }

        protected override bool CanInvokeCommand(object obj)
        {
            return this.analyzeJobPanel.BeginAnalyzeCommand.CanExecute(obj);
        }

        protected override void InvokeCommand(object obj)
        {
            this.analyzeJobPanel.BeginAnalyzeCommand.Execute(obj);
        }
    }
}