namespace SyncPro.UI.ViewModels.Adapters
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows.Input;

    using SyncPro.Adapters.WindowsFileSystem;
    using SyncPro.UI.FolderBrowser;
    using SyncPro.UI.Framework.MVVM;
    using SyncPro.UI.Framework.Validation.Rules;

    public class WindowsFileSystemAdapterViewModel : SyncAdapterViewModel
    {
        public static readonly Guid TargetTypeId = WindowsFileSystemAdapter.TargetTypeId;

        public override string DisplayName => "Folder On Local Computer";

        public override string ShortDisplayName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(this.DestinationPath) && this.DestinationPath.StartsWith("\\"))
                {
                    return "Network Share";
                }

                return "Local Computer";
            }
        }

        public override string LogoImage => "/SyncPro.UI;component/Resources/ProviderLogos/microsoft_windows.png";

        public WindowsFileSystemAdapter Adapter => (WindowsFileSystemAdapter) this.AdapterBase;

        [StringNotNullorEmptyValidationRule]
        public override string DestinationPath
        {
            get { return this.Adapter.Config.RootDirectory; }
            set
            {
                this.SetPropertyDelegated("DestinationPath", this.Adapter.Config.RootDirectory, value, () =>
                {
                    this.Adapter.Config.RootDirectory = value;
                });
            }
        }

        public ICommand BrowsePathCommand { get; private set; }

        public WindowsFileSystemAdapterViewModel(WindowsFileSystemAdapter adapter)
            : base(adapter)
        {
            this.BrowsePathCommand = new DelegatedCommand(this.BrowsePath);
        }

        public static WindowsFileSystemAdapterViewModel CreateFromRelationship(SyncRelationshipViewModel relationship, bool isSourceAdapter)
        {
            ISyncTargetViewModel existingAdapter = isSourceAdapter ? relationship.SyncSourceAdapter : relationship.SyncDestinationAdapter;
            WindowsFileSystemAdapterViewModel model = existingAdapter as WindowsFileSystemAdapterViewModel;
            if (model != null)
            {
                return model;
            }

            // Create a temporary adapter. This will only be committed to the DB when the user actually creates the relationship.
            WindowsFileSystemAdapterViewModel adapterViewModel = relationship.CreateAdapterViewModel<WindowsFileSystemAdapterViewModel>();

            // If we are creating a new adapter view model (and adapter), set the IsOriginator property
            adapterViewModel.Adapter.Configuration.IsOriginator = isSourceAdapter;

            return adapterViewModel;

        }

        private FolderBrowserViewModel viewModel;
        private void BrowsePath(object obj)
        {
            FolderBrowserWindow window = new FolderBrowserWindow();
            if (this.viewModel == null)
            {
                this.viewModel = new FolderBrowserViewModel(this.AdapterBase)
                {
                    Message = this.Adapter.Configuration.IsOriginator ?
                        "Select the source folder" :
                        "Select the destination folder"
                };

                if (!string.IsNullOrWhiteSpace(this.DestinationPath))
                {
                    // TODO: Fix
                    //this.viewModel.SelectedPath = this.DestinationPath;
                }
            }

            if (!this.viewModel.RootFolders.Any())
            {
                this.viewModel.LoadRootFolders();
            }

            window.DataContext = this.viewModel;

            if (!string.IsNullOrEmpty(this.DestinationPath))
            {
                ThreadingHelper.StartBackgroundTask(
                    (s, t) =>
                    {
                        foreach (FolderViewModel root in this.viewModel.RootFolders)
                        {
                            FolderViewModel folder = this.GetFolderByPath(root, this.DestinationPath.Split('\\').ToList());
                            if (folder != null)
                            {
                                folder.IsExpanded = true;
                                folder.IsSelected = true;
                                break;
                            }
                        }
                    });
            }

            bool? dialogResult = window.ShowDialog();

            if (dialogResult == true)
            {
                this.DestinationPath = this.viewModel.SelectedFolder.GetPath();
                //this.DestinationPath = this.viewModel.SelectedPath;
                //this.SetDestinationPath(this.viewModel.SelectedPath);
            }
        }

        private FolderViewModel GetFolderByPath(FolderViewModel folder, IList<string> path)
        {
            if (!path.Any())
            {
                return folder;
            }

            if (!folder.AreSubFoldersLoaded)
            {
                return null;
            }

            var subFolder = folder.SubFolders.FirstOrDefault(f => f.Name.Equals(path.First()));

            if (subFolder == null)
            {
                return null;
            }

            path.RemoveAt(0);
            return this.GetFolderByPath(subFolder, path);
        }

        public override void LoadContext()
        {
            this.DestinationPath = this.Adapter.Config.RootDirectory;
            //this.SetDestinationPath(this.AdapterBase.RootDirectory);
        }

        public override void SaveContext()
        {
            this.Adapter.Config.RootDirectory = this.DestinationPath;
        }

        public override Type GetAdapterType()
        {
            return typeof(WindowsFileSystemAdapter);
        }
    }
}