namespace SyncPro.UI.Navigation.MenuCommands
{
    using System.Diagnostics;
    using System.Linq;
    using SyncPro.Runtime;
    using SyncPro.UI.Navigation.ViewModels;
    using SyncPro.UI.ViewModels;

    public class AnalyzeRelationshipMenuCommand : NavigationItemMenuCommand
    {
        private readonly SyncRelationshipViewModel relationship;

        public AnalyzeRelationshipMenuCommand(SyncRelationshipViewModel relationship)
            : base("ANALYZE", "/SyncPro.UI;component/Resources/Graphics/select_invert_16.png")
        {
            this.relationship = relationship;
        }

        protected override bool CanInvokeCommand(object obj)
        {
            return this.relationship.State == SyncRelationshipState.Idle;
        }

        protected override void InvokeCommand(object obj)
        {
            // Find the navigation tree item for this relationship
            SyncRelationshipNodeViewModel relatonshipNavItem =
                App.Current.MainWindowsViewModel.NavigationItems.OfType<SyncRelationshipNodeViewModel>().FirstOrDefault(
                    n => n.Item == this.relationship);

            Debug.Assert(relatonshipNavItem != null, "relatonshipNavItem != null");

            // Check if an Analyze item is already present under this relationship
            NavigationNodeViewModel analyzeItem =
                relatonshipNavItem.Children.OfType<AnalyzeResultsNodeViewModel>().FirstOrDefault();

            // An analyze item is not present, so add a new one.
            if (analyzeItem == null)
            {
                SyncJobPanelViewModel viewModel = new SyncJobPanelViewModel(this.relationship)
                {
                    IsAnalyzeOnlyMode = true
                };

                analyzeItem = new AnalyzeResultsNodeViewModel(relatonshipNavItem, viewModel);
                relatonshipNavItem.Children.Add(analyzeItem);
            }

            analyzeItem.IsSelected = true;
            SyncJobPanelViewModel syncJobViewModel = (SyncJobPanelViewModel) analyzeItem.Item;
            if (syncJobViewModel.BeginAnalyzeCommand.CanExecute(null))
            {
                syncJobViewModel.BeginAnalyzeCommand.Execute(null);
            }
        }
    }
}