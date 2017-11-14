namespace SyncPro.UI.Navigation.MenuCommands
{
    using SyncPro.UI.ViewModels;

    public class ClosePanelMenuCommand : NavigationItemMenuCommand
    {
        private readonly SyncRelationshipViewModel relationship;
        private readonly NavigationNodeViewModel viewModel;

        public ClosePanelMenuCommand(SyncRelationshipViewModel relationship, NavigationNodeViewModel navigationNodeViewModel)
            //: base("REMOVE RELATIONSHIP", "/SyncPro.UI;component/Resources/Graphics/delete_16.png")
            : base("CLOSE", "/SyncPro.UI;component/Resources/Graphics/close_window_16.png")
        {
            this.relationship = relationship;
            this.viewModel = navigationNodeViewModel;
        }

        protected override bool CanInvokeCommand(object obj)
        {
            return !this.relationship.IsSyncActive;
        }

        protected override void InvokeCommand(object obj)
        {
            this.viewModel.Parent.Children.Remove(this.viewModel);
        }
    }
}