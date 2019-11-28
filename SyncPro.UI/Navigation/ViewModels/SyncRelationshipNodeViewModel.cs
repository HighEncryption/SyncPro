namespace SyncPro.UI.Navigation.ViewModels
{
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Linq;
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

                if (args.PropertyName == nameof(this.Relationship.ActiveJob) &&
                    this.Relationship.ActiveJob != null)
                {
                    this.Relationship.ActiveJob.PropertyChanged += ActiveJobOnPropertyChanged;

                    this.ProgressIsIndeterminate = this.Relationship.ActiveJob.IsProgressIndeterminate;

                    // Check if an Analyze item is already present under this relationship
                    NavigationNodeViewModel analyzeItem =
                        this.Children.OfType<AnalyzeJobNodeViewModel>().FirstOrDefault();

                    // An analyze item is not present, so add a new one.
                    if (analyzeItem == null)
                    {
                        AnalyzeJobPanelViewModel viewModel = new AnalyzeJobPanelViewModel(this.Relationship);

                        analyzeItem = new AnalyzeJobNodeViewModel(this, viewModel);
                        App.DispatcherInvoke(() => this.Children.Add(analyzeItem));
                    }

                    if (this.IsSelected)
                    {
                        App.DispatcherInvoke(() => analyzeItem.IsSelected = true);
                    }
                }

                if (args.PropertyName == nameof(this.Relationship.Name))
                {
                    this.Name = this.Relationship.Name;
                }
            };

            this.SetNavigationState();
        }

        private void ActiveJobOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(this.Relationship.ActiveJob.ProgressValue))
            {
                this.ProgressValue = this.Relationship.ActiveJob.ProgressValue * 100;
            }

            if (e.PropertyName == nameof(this.Relationship.ActiveJob.IsProgressIndeterminate))
            {
                this.ProgressIsIndeterminate = this.Relationship.ActiveJob.IsProgressIndeterminate;
            }
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