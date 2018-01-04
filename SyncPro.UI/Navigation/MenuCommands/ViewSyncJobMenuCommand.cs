namespace SyncPro.UI.Navigation.MenuCommands
{
    using SyncPro.UI.ViewModels;

    public class ViewSyncJobMenuCommand : NavigationItemMenuCommand
    {
        private readonly SyncRelationshipViewModel relationship;
        private readonly SyncJobViewModel syncJobViewModel;

        public ViewSyncJobMenuCommand(SyncRelationshipViewModel syncRelationship, SyncJobViewModel syncJobViewModel)
            //: base("REMOVE RELATIONSHIP", "/SyncPro.UI;component/Resources/Graphics/delete_16.png")
            : base("VIEW SYNC JOB", "/SyncPro.UI;component/Resources/Graphics/list_16.png")
        {
            this.relationship = syncRelationship;
            this.syncJobViewModel = syncJobViewModel;
        }

        protected override bool CanInvokeCommand(object obj)
        {
            return this.syncJobViewModel.ViewSyncJobCommand.CanExecute(obj);
        }

        protected override void InvokeCommand(object obj)
        {
            this.syncJobViewModel.ViewSyncJobCommand.Execute(obj);
        }
    }
}