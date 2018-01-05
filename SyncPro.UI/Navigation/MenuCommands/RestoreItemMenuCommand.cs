namespace SyncPro.UI.Navigation.MenuCommands
{
    using System;
    using System.ComponentModel;
    using System.Linq;
    using System.Windows.Forms;

    using SyncPro.UI.Navigation.ViewModels;
    using SyncPro.UI.ViewModels;

    public class RestoreItemMenuCommand : NavigationItemMenuCommand
    {
        private readonly IFolderNodeViewModel nodeViewModel;
        private readonly SyncRelationshipViewModel relationship;

        public RestoreItemMenuCommand(IFolderNodeViewModel nodeViewModel, SyncRelationshipViewModel relationship)
            : base("RESTORE FILE", "/SyncPro.UI;component/Resources/Graphics/install_16.png")
        {
            this.nodeViewModel = nodeViewModel;
            this.relationship = relationship;
            this.nodeViewModel.PropertyChanged += this.NodeViewModelPropertyChanged;
        }

        private void NodeViewModelPropertyChanged(Object sender, PropertyChangedEventArgs e)
        {
            if (this.nodeViewModel.SelectedChildEntries == null ||
                !this.nodeViewModel.SelectedChildEntries.Any())
            {
                this.Header = "RESTORE FILE";
            }
            else if (this.nodeViewModel.SelectedChildEntries.Count > 1)
            {
                this.Header = "RESTORE FILES";
            }
            else if (this.nodeViewModel.SelectedChildEntries.First().IsDirectory)
            {
                this.Header = "RESTORE FOLDER";
            }
            else
            {
                this.Header = "RESTORE FILE";
            }
        }

        protected override bool CanInvokeCommand(object obj)
        {
            return this.nodeViewModel.SelectedChildEntries != null &&
                   this.nodeViewModel.SelectedChildEntries.Any();
        }

        protected override void InvokeCommand(object obj)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "Select the folder where items will be restored to.";
                System.Windows.Forms.DialogResult result = dialog.ShowDialog();

                if (result != DialogResult.OK)
                {
                    return;
                }

#pragma warning disable 4014
                this.relationship.RestoreFilesAsync(
                    this.nodeViewModel.SelectedChildEntries,
                    dialog.SelectedPath);
#pragma warning restore 4014
            }
        }
    }
}