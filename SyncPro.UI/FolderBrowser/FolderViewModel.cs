namespace SyncPro.UI.FolderBrowser
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Adapters;

    using SyncPro.Adapters.MicrosoftOneDrive;
    using SyncPro.UI.Framework;

    public class FolderViewModel : NotifyPropertyChangedSlim
    {
        static readonly FolderViewModel Placeholder = new FolderViewModel();

        private readonly FolderBrowserViewModel browserViewModel;

        public FolderViewModel Parent { get; private set; }

        private readonly IAdapterItem folder;

        public ImageSource GetIconSource(bool expanded)
        {
            string resourcePath = expanded ? "/Resources/Graphics/folder_open_16.png" : "/Resources/Graphics/folder_16.png";

            if (this.folder.Adapter.GetTargetTypeId() == OneDriveAdapter.TargetTypeId)
            {
                resourcePath = "/Resources/Graphics/Microsoft-OneDrive-icon.png";
            }

            var uri =  new Uri(resourcePath, UriKind.Relative);
            return new BitmapImage(uri);
        }

        public FolderViewModel(FolderBrowserViewModel browserViewModel, FolderViewModel parent, IAdapterItem folder)
        {
            this.browserViewModel = browserViewModel;
            this.Parent = parent;
            this.folder = folder;
            this.DisplayName = folder.Name;
            this.ErrorMessage = folder.ErrorMessage;

            if (string.IsNullOrWhiteSpace(this.ErrorMessage))
            {
                this.SubFolders.Add(Placeholder);
            }

            this.IconSource = this.GetIconSource(false);

        }

        private FolderViewModel()
        {
        }

        public string Name
        {
            //get { return this.folder.DisplayName; }
            get { return this.DisplayName; }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string displayName;

        public string DisplayName
        {
            get { return this.displayName; }
            set { this.SetProperty("DisplayName", ref this.displayName, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private ImageSource iconSource;

        public ImageSource IconSource
        {
            get { return this.iconSource; }
            set { this.SetProperty("IconSource", ref this.iconSource, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool isSelected;

        public bool IsSelected
        {
            get { return this.isSelected; }
            set
            {
                if (this.SetProperty("IsSelected", ref this.isSelected, value) && value)
                {
                    this.browserViewModel.SelectedFolder = this;
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool isExpanded;

        public bool IsExpanded
        {
            get { return this.isExpanded; }
            set
            {
                if (this.SetProperty("IsExpanded", ref this.isExpanded, value))
                {
                    if (value)
                    {
                        // Recursivly expand up the tree
                        if (this.Parent != null)
                        {
                            this.Parent.IsExpanded = true;
                        }

                        if (!this.AreSubFoldersLoaded)
                        {
                            this.SubFolders.Clear();
                            this.LoadSubFolders();
                        }
                    }

                    this.IconSource = this.GetIconSource(value && this.SubFolders.Any());
                }
            }
        }

        //public bool IsHidden { get { return this.folder.IsHidden; } }
        public bool IsHidden { get { return false; } }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string errorMessage;

        public string ErrorMessage
        {
            get { return this.errorMessage; }
            set { this.SetProperty("ErrorMessage", ref this.errorMessage, value); }
        }

        public string GetPath()
        {
            return this.folder.FullName;
        }

        private ObservableCollection<FolderViewModel> subFolders;

        public ObservableCollection<FolderViewModel> SubFolders
        {
            get { return this.subFolders ?? (this.subFolders = new ObservableCollection<FolderViewModel>()); }
        }

        public bool AreSubFoldersLoaded
        {
            get { return this.SubFolders.Count != 1 || this.SubFolders.First() != Placeholder; }
        }

        public bool HasSubFolders
        {
            get { return this.AreSubFoldersLoaded && this.SubFolders.Any(); }
        }

        public virtual void LoadSubFolders()
        {
            ThreadingHelper.StartBackgroundTask(this.LoadFoldersInternal);
        }

        private void LoadFoldersInternal(TaskScheduler scheduler, CancellationToken token)
        {
            IEnumerable<IAdapterItem> folders;

            try
            {
                folders = this.folder.Adapter.GetAdapterItems(this.folder).Where(i => i.ItemType == SyncAdapterItemType.Directory);
            }
            catch (Exception exception)
            {
                this.ErrorMessage = exception.Message;
                return;
            }

            TaskFactory factory = new TaskFactory(scheduler);
            factory.StartNew(
                () =>
                {
                    this.SubFolders.Clear();
                    foreach (IAdapterItem f in folders)
                    {
                        this.SubFolders.Add(new FolderViewModel(this.browserViewModel, this, f));
                    }

                    this.RaisePropertyChanged("AreSubFoldersLoaded");
                    this.RaisePropertyChanged("HasSubFolders");
                },
                token);
        }
    }
}