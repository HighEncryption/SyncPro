namespace SyncPro.UI.Navigation.ViewModels
{
    using SyncPro.UI.Navigation.MenuCommands;

    public class AnalyzeJobNodeViewModel : NavigationNodeViewModel
    {
        public AnalyzeJobNodeViewModel(NavigationNodeViewModel parent, AnalyzeJobPanelViewModel viewModel)
            : base(parent, viewModel) 
        {
            this.Name = "Analyze";
            this.IconImageSource = "/SyncPro.UI;component/Resources/Graphics/select_invert_16.png";

            this.ClosePanelCommand = new ClosePanelMenuCommand(viewModel.Relationship, this);

            this.MenuCommands.Add(new ReanalyzeMenuCommand(viewModel));
            this.MenuCommands.Add(new SynchronzieNowMenuCommand(viewModel));
            this.MenuCommands.Add(new CancelOperationMenuCommand(viewModel));
        }

        public override void Closing()
        {
            ((AnalyzeJobPanelViewModel) this.Item).Closing();
        }
    }
}