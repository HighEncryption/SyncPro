namespace SyncPro.UI.Navigation.ViewModels
{
    using System.Diagnostics;
    using System.Windows.Input;

    using SyncPro.Adapters.WindowsFileSystem;
    using SyncPro.Runtime;
    using SyncPro.UI.Navigation.MenuCommands;
    using SyncPro.UI.ViewModels;

    public class SyncRelationshipNodeViewModel : NavigationNodeViewModel
    {
        public SyncRelationshipViewModel Relationship { get; }

        public SyncRelationshipNodeViewModel(NavigationNodeViewModel parent, SyncRelationshipViewModel relationship) 
            : base(parent, relationship)
        {
            Debug.Assert(relationship != null, "relationship != null");

            this.Relationship = relationship;
            this.Name = relationship.Name;
            this.IconImageSource = "/SyncPro.UI;component/Resources/Graphics/sort2_16.png";
            this.IsExpanded = true; // Expanded by default
            this.IsExpanderVisible = false;

            // Add the default child navigation nodes for this relationship
            this.Children.Add(new SyncHistoryNodeViewModel(this, relationship));
            this.Children.Add(new FilesAndFoldersNodeViewModel(this, relationship));

            this.MenuCommands.Add(new ChangeConfigurationMenuCommand(relationship));
            this.MenuCommands.Add(new RemoveRelationshipMenuCommand(relationship));
            this.MenuCommands.Add(new AnalyzeRelationshipMenuCommand(relationship));
            this.MenuCommands.Add(new BeginSyncMenuCommand(relationship));

            if (this.Relationship.SyncSourceAdapter?.AdapterBase is WindowsFileSystemAdapter)
            {
                this.MenuCommands.Add(
                    new OpenFolderMenuCommand(this.Relationship.SyncSourceAdapter));
            }

            if (this.Relationship.SyncDestinationAdapter?.AdapterBase is WindowsFileSystemAdapter)
            {
                this.MenuCommands.Add(
                    new OpenFolderMenuCommand(this.Relationship.SyncDestinationAdapter));
            }

            this.Relationship.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(this.Relationship.State))
                {
                    this.SetNavigationState();

                    // State changes for the relationship almost always require that the UI be updated after the 
                    // state change occurs. Rather than tracking down all of the places where this would be 
                    // needed, simply handle it here.
                    App.DispatcherInvoke(CommandManager.InvalidateRequerySuggested);
                }

                if (args.PropertyName == nameof(this.Relationship.Name))
                {
                    this.Name = this.Relationship.Name;
                }
            };

            this.SetNavigationState();
        }

        private void SetNavigationState()
        {
            if (this.Relationship.State == SyncRelationshipState.Initializing)
            {
                this.ShowStatusIcon = false;
                this.ShowProgress = true;
                this.ProgressIsIndeterminate = true;
            }
            else if (this.Relationship.State == SyncRelationshipState.Idle)
            {
                this.ShowStatusIcon = false;
                this.ShowProgress = false;
                this.ProgressIsIndeterminate = false; 
            }
            else if (this.Relationship.State == SyncRelationshipState.Running)
            {
                this.ShowStatusIcon = false;
                this.ShowProgress = true;
                this.ProgressIsIndeterminate = true;
            }
            else if (this.Relationship.State == SyncRelationshipState.Error)
            {
                this.ShowStatusIcon = true;
                this.ShowProgress = false;
                this.StatusIconImageSource = "/SyncPro.UI;component/Resources/Graphics/icon_exclaim.png";
            }
        }
    }
}