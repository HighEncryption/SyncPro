namespace SyncPro.UI.Navigation.MenuCommands
{
    using SyncPro.UI.ViewModels;

    public class ViewSyncRunMenuCommand : NavigationItemMenuCommand
    {
        private readonly SyncRelationshipViewModel relationship;
        private readonly SyncRunViewModel syncRunViewModel;

        public ViewSyncRunMenuCommand(SyncRelationshipViewModel syncRelationship, SyncRunViewModel syncRunViewModel)
            //: base("REMOVE RELATIONSHIP", "/SyncPro.UI;component/Resources/Graphics/delete_16.png")
            : base("VIEW SYNC RUN", "/SyncPro.UI;component/Resources/Graphics/list_16.png")
        {
            this.relationship = syncRelationship;
            this.syncRunViewModel = syncRunViewModel;
        }

        protected override bool CanInvokeCommand(object obj)
        {
            return this.syncRunViewModel.ViewSyncRunCommand.CanExecute(obj);
        }

        protected override void InvokeCommand(object obj)
        {
            this.syncRunViewModel.ViewSyncRunCommand.Execute(obj);
        }
    }
}