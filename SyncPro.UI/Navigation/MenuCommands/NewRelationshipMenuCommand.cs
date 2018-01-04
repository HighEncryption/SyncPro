namespace SyncPro.UI.Navigation.MenuCommands
{
    using System;

    using SyncPro.Runtime;
    using SyncPro.UI.RelationshipEditor;
    using SyncPro.UI.ViewModels;

    public class NewRelationshipMenuCommand : NavigationItemMenuCommand
    {
        public NewRelationshipMenuCommand()
            : base("NEW RELATIONSHIP", "/SyncPro.UI;component/Resources/Graphics/add_16.png")
        {
        }

        protected override bool CanInvokeCommand(object obj)
        {
            return true;
        }

        protected override void InvokeCommand(object obj)
        {
            SyncRelationship newRelationship = SyncRelationship.Create();

            newRelationship.Description =
                string.Format("Sync relationship created on {0:MM/dd/yyyy} at {0:h:mm tt}", DateTime.Now);

            // The relationship object is not populated, so dont load the context in the view model
            SyncRelationshipViewModel syncRelationshipViewModel = new SyncRelationshipViewModel(newRelationship, false);

            RelationshipEditorViewModel viewModel = new RelationshipEditorViewModel(syncRelationshipViewModel, false);
            EditorWindow editorWindow = new EditorWindow { DataContext = viewModel };

            editorWindow.ShowDialog();
        }
    }
}