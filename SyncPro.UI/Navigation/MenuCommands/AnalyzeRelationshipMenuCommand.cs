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
            this.relationship.AnalyzeNowCommand.Execute(obj);


            //// Find the navigation tree item for this relationship
            //SyncRelationshipNodeViewModel relationshipNavItem =
            //    App.Current.MainWindowsViewModel.NavigationItems.OfType<SyncRelationshipNodeViewModel>().FirstOrDefault(
            //        n => n.Item == this.relationship);

            //Debug.Assert(relationshipNavItem != null, "relationshipNavItem != null");

            //// Check if an Analyze item is already present under this relationship
            //NavigationNodeViewModel analyzeItem =
            //    relationshipNavItem.Children.OfType<AnalyzeJobNodeViewModel>().FirstOrDefault();

            //// An analyze item is not present, so add a new one.
            //if (analyzeItem == null)
            //{
            //    AnalyzeJobPanelViewModel viewModel = new AnalyzeJobPanelViewModel(this.relationship);

            //    analyzeItem = new AnalyzeJobNodeViewModel(relationshipNavItem, viewModel);
            //    relationshipNavItem.Children.Add(analyzeItem);
            //}

            //analyzeItem.IsSelected = true;
            //AnalyzeJobPanelViewModel analyzeJobViewModel = (AnalyzeJobPanelViewModel) analyzeItem.Item;
            //if (analyzeJobViewModel.BeginAnalyzeCommand.CanExecute(null))
            //{
            //    analyzeJobViewModel.BeginAnalyzeCommand.Execute(null);
            //}
        }
    }
}