namespace SyncPro.UI.Navigation.MenuCommands
{
    using System;
    using System.ComponentModel;
    using System.Linq;

    using SyncPro.UI.Dialogs;
    using SyncPro.UI.Navigation.ViewModels;
    using SyncPro.UI.ViewModels;

    public class RestoreItemMenuCommand : NavigationItemMenuCommand
    {
        private readonly IFolderNodeViewModel nodeViewModel;
        private readonly SyncRelationshipViewModel relationship;

        private const string headerRestoreFile = "Restore File";
        private const string headerRestoreFiles = "Restore Files";
        private const string headerRestoreFolder = "Restore Folder";

        private string dialogHeader;

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
                this.dialogHeader = headerRestoreFile;
            }
            else if (this.nodeViewModel.SelectedChildEntries.Count > 1)
            {
                this.dialogHeader = headerRestoreFiles;
            }
            else if (this.nodeViewModel.SelectedChildEntries.First().IsDirectory)
            {
                this.dialogHeader = headerRestoreFolder;
            }
            else
            {
                this.dialogHeader = headerRestoreFile;
            }

            this.Header = this.dialogHeader.ToUpperInvariant();
        }

        protected override bool CanInvokeCommand(object obj)
        {
            return this.nodeViewModel.SelectedChildEntries != null &&
                   this.nodeViewModel.SelectedChildEntries.Any();
        }

        protected override void InvokeCommand(object obj)
        {
            var destAdapter = this.relationship.SyncSourceAdapter;

            RestoreItemsDialogViewModel dialogViewModel = new RestoreItemsDialogViewModel()
            {
                DialogHeader = this.dialogHeader,
                DialogDescription = string.Format(
                    "Items will be restored from {0} ({1}) to the location selected below.",
                    destAdapter.DestinationPath,
                    destAdapter.DisplayName)
            };

            RestoreItemsDialog dialog = new RestoreItemsDialog
            {
                DataContext = dialogViewModel
            };

            if (dialog.ShowDialog() == true)
            {
#pragma warning disable 4014
                this.relationship.RestoreFilesAsync(
                    this.nodeViewModel.SelectedChildEntries,
                    dialogViewModel.RestoreBrowsePath);
#pragma warning restore 4014
            }
        }
    }
}