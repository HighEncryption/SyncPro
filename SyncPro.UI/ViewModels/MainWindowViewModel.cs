namespace SyncPro.UI.ViewModels
{
    using System;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Windows.Input;

    using SyncPro.Runtime;
    using SyncPro.Tracing;
    using SyncPro.UI.Framework;
    using SyncPro.UI.Framework.MVVM;
    using SyncPro.UI.Navigation;
    using SyncPro.UI.Navigation.ViewModels;
    using SyncPro.UI.RelationshipEditor;

    public class MainWindowViewModel : ViewModelBase, IRequestClose
    {
        public ICommand StartPowerShellCommand { get; }

        public ICommand CreateRelationshipCommand { get; }

        public ICommand CloseWindowCommand { get; }

        public string WindowTitle { get; set; }

        private ObservableCollection<ViewModelBase> syncRelationships;

        public ObservableCollection<ViewModelBase> SyncRelationships
            => this.syncRelationships ?? (this.syncRelationships = new ObservableCollection<ViewModelBase>());

        private ObservableCollection<NavigationNodeViewModel> navigationItems;

        public ObservableCollection<NavigationNodeViewModel> NavigationItems
            => this.navigationItems ?? (this.navigationItems = new ObservableCollection<NavigationNodeViewModel>());

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private ViewModelBase selectedSyncRelationship;

        // The sync relationship selected in the dashboard
        public ViewModelBase SelectedSyncRelationship
        {
            get { return this.selectedSyncRelationship; }
            set
            {
                if (this.SetProperty(ref this.selectedSyncRelationship, value) && value != null)
                {
                    SyncRelationshipNodeViewModel relationshipViewModel = this.NavigationItems
                        .OfType<SyncRelationshipNodeViewModel>()
                        .Single(vm => vm.Item == value);

                    this.SelectedNavigationItem = relationshipViewModel;
                    this.selectedSyncRelationship = null;
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private NavigationNodeViewModel selectedNavigationItem;

        public NavigationNodeViewModel SelectedNavigationItem
        {
            get { return this.selectedNavigationItem; }
            set
            {
                if (this.SetProperty(ref this.selectedNavigationItem, value))
                {
                    if (value == null)
                    {
                        //this.CurrentNavigationRelationship = null;
                        this.CurrentNavigationRoot = null;
                        return;
                    }

                    if (value is DashboardNodeViewModel)
                    {
                        this.CurrentNavigationRoot = value;
                        Global.SelectedSyncRelationship = null;
                        return;
                    }

                    NavigationNodeViewModel element = value;
                    while (!(element is SyncRelationshipNodeViewModel))
                    {
                        Debug.Assert(element.Parent != null, "element.Parent != null");
                        element = element.Parent;
                    }

                    this.CurrentNavigationRoot = element;
                    value.IsSelected = true;

                    var syncRelationshipNode = element as SyncRelationshipNodeViewModel;
                    if (syncRelationshipNode != null)
                    {
                        Global.SelectedSyncRelationship =
                            syncRelationshipNode.Relationship.GetSyncRelationship();
                    }
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private NavigationNodeViewModel currentNavigationRoot;

        public NavigationNodeViewModel CurrentNavigationRoot
        {
            get { return this.currentNavigationRoot; }
            set { this.SetProperty(ref this.currentNavigationRoot, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string searchText;

        public string SearchText
        {
            get { return this.searchText; }
            set { this.SetProperty(ref this.searchText, value); }
        }

        public ICommand BeginSearchCommand { get; }

        public MainWindowViewModel()
        {
            this.StartPowerShellCommand = new DelegatedCommand(this.StartPowerShell);
            this.CreateRelationshipCommand = new DelegatedCommand(this.CreateRelationship);
            this.CloseWindowCommand = new DelegatedCommand(this.CloseWindow);
            this.BeginSearchCommand = new DelegatedCommand(this.BeginSearch);

            this.WindowTitle = GetAssemblyVersionString();
        }

        private static string GetAssemblyVersionString()
        {
            var executingAssembly = Assembly.GetExecutingAssembly();

            /*
             * Notes on compiled git information
             * By including a build action in the project (shown below), we can include information
             * about the state of the code (such as the last commit ID) from when the when the 
             * project was built. The command is:
             * 
             * git describe --always --long --dirty >$(ProjectDir)\version.txt
             * 
             * Add the command as a pre-build step, then build once to generate the file. Add
             * the file to the root of the project as an embedded resource.
             * 
             * From the code, we can then read the state of the git repository at build time. The
             * output from the command has one of the following two format:
             * 
             * {commidId}[-dirty]
             *   or
             * {latestTag}-{commitDistanceFromTag}-{commitId}[-dirty]
             * 
             * If there is a tag in the history, the second form will be used, which includes
             * the tag and the number of commits from that tag to HEAD (or where the code was
             * built). If there are no tags, the first form is used.
             * 
             * If there were any uncomitted changes (staged or unstaged), then the '-dirty' string
             * will be appended to the commit ID.
             */
            string gitStatus;
            using (Stream stream = executingAssembly.GetManifestResourceStream("SyncPro.UI.version.txt"))
            {
                Pre.Assert(stream != null, "stream != null");
                using (StreamReader reader = new StreamReader(stream))
                {
                    gitStatus = reader.ReadToEnd().Trim();
                }
            }

            // Check if the output includes '-dirty' at the end to indicate the build state was dirty
            bool isDirty = false;
            if (gitStatus.EndsWith("-dirty"))
            {
                isDirty = true;
                gitStatus = gitStatus.Substring(0, gitStatus.Length - 6);
            }

            // If the remaining output does not contain any dashes, then the non-tag format was used
            // and we know that the remaining string is the commit ID
            string tag = null;
            string distanceFromTag = null;
            string lastCommitId;
            if (!gitStatus.Contains("-"))
            {
                lastCommitId = gitStatus;
            }
            else
            {
                // Split the remaining string and parse out the individual strings. Note that the
                // tag itself may contain a dash, so we need to work backward from the end of the
                // string.
                string[] parts = gitStatus.Split('-');
                lastCommitId = parts[parts.Length - 1];
                distanceFromTag = parts[parts.Length - 2];
                tag = string.Join("-", parts.Take(parts.Length - 2));
            }

            Logger.Info(
                "Build information: Hash={0}, Tag={1}, TagDistance={2}, Dirty={3}",
                lastCommitId,
                tag,
                distanceFromTag,
                isDirty);

            return string.Format(
                "SyncPro (build {0}-{1}{2})", 
                executingAssembly.GetName().Version, 
                lastCommitId,
                isDirty ? "*" : null);
        }

        private void BeginSearch(object obj)
        {
            SyncRelationshipNodeViewModel syncRelationshipNode =
                this.CurrentNavigationRoot as SyncRelationshipNodeViewModel;

            if (syncRelationshipNode != null)
            {
                // TODO: The second parameter is probably wrong here.. likely need a custom view model
                SearchResultsNodeViewModel searchResultsNode = new SearchResultsNodeViewModel(syncRelationshipNode,
                    syncRelationshipNode.Relationship);

                this.CurrentNavigationRoot.Children.Add(searchResultsNode);
                searchResultsNode.IsSelected = true;
            }
        }

        private void StartPowerShell(object obj)
        {
            Logger.Info("Starting PowerShell window");

            PowerShell.RuntimeHost.Start();
        }

        private void CreateRelationship(object obj)
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

        private void CloseWindow(object obj)
        {
            this.RequestClose?.Invoke(this, new RequestCloseEventArgs());
        }

        #region IRequestClose

        public event RequestCloseEventHandler RequestClose;

        public void WindowClosing(CancelEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            {
                App.Current.ShutdownApplicationCommand.Execute(null);
            }

            if (this.MustClose)
            {
                // We are being forced to close, so don't show the confirmation message.
                e.Cancel = false;
            }
        }

        public bool MustClose { get; set; }

        #endregion
    }
}