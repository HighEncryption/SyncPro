namespace SyncPro.UI.Navigation.MenuCommands
{
    using System.Windows;

    using SyncPro.UI.ViewModels;

    public class RemoveRelationshipMenuCommand : NavigationItemMenuCommand
    {
        private readonly SyncRelationshipViewModel relationship;

        public RemoveRelationshipMenuCommand(SyncRelationshipViewModel relationship)
            : base("REMOVE", "/SyncPro.UI;component/Resources/Graphics/delete_16.png")
        {
            this.relationship = relationship;
        }

        protected override bool CanInvokeCommand(object obj)
        {
            return this.relationship.DeleteRelationshipCommand.CanExecute(obj);
        }

        protected override void InvokeCommand(object obj)
        {
            MessageBoxResult result = MessageBox.Show(
                "Are you sure you want to remove this relationship? No files will be removed, but all file history WILL be removed!",
                "Remove Relationship",
                MessageBoxButton.YesNo, MessageBoxImage.Exclamation);

            if (result == MessageBoxResult.Yes)
            {
                this.relationship.DeleteRelationshipCommand.Execute(obj);
            }
        }
    }
}