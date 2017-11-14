namespace SyncPro.UI.Navigation.ViewModels
{
    using System.Linq;

    using SyncPro.Runtime;
    using SyncPro.UI.Navigation.MenuCommands;

    public class AnalyzeResultsNodeViewModel : NavigationNodeViewModel
    {
        private readonly SyncRunPanelViewModel viewModel;

        public AnalyzeResultsNodeViewModel(NavigationNodeViewModel parent, SyncRunPanelViewModel viewModel)
            : base(parent, viewModel) 
        {
            this.viewModel = viewModel;
            this.Name = "Analyze";
            this.IconImageSource = "/SyncPro.UI;component/Resources/Graphics/select_invert_16.png";

            this.ClosePanelCommand = new ClosePanelMenuCommand(viewModel.Relationship, this);

            this.MenuCommands.Add(new ReanalyzeMenuCommand(viewModel));
            this.MenuCommands.Add(new SynchronzieNowMenuCommand(viewModel));
            this.MenuCommands.Add(new CancelOperationMenuCommand(viewModel));
        }
    }
}