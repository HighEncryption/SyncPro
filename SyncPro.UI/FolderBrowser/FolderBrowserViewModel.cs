namespace SyncPro.UI.FolderBrowser
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Input;

    using SyncPro.Adapters;
    using SyncPro.UI.Framework;
    using SyncPro.UI.Framework.MVVM;

    public class FolderBrowserViewModel : ViewModelBase, IRequestClose
    {
        public ICommand OKCommand { get; }

        public ICommand CancelCommand { get; }

        public ICommand CloseWindowCommand { get; }

        private readonly AdapterBase syncAdapter;

        //public string SelectedPath { get; set; }

        public string Message { get; set; }

        private FolderViewModel selectedFolder;

        public FolderViewModel SelectedFolder
        {
            get { return this.selectedFolder;  }
            set { this.SetProperty(ref this.selectedFolder, value); }
        }

        private ObservableCollection<FolderViewModel> rootFolders;

        public ObservableCollection<FolderViewModel> RootFolders => 
            this.rootFolders ?? (this.rootFolders = new ObservableCollection<FolderViewModel>());

        public void LoadRootFolders()
        {
            ThreadingHelper.StartBackgroundTask(this.LoadRootFoldersInternal);
        }

        private void LoadRootFoldersInternal(TaskScheduler scheduler, CancellationToken token)
        {
            IEnumerable<IAdapterItem> folders = this.syncAdapter.GetAdapterItems(null).Where(i => i.ItemType == SyncAdapterItemType.Directory);

            TaskFactory factory = new TaskFactory(scheduler);
            Task t = factory.StartNew(
                () =>
                {
                    this.RootFolders.Clear();
                    foreach (IAdapterItem f in folders)
                    {
                        this.RootFolders.Add(new FolderViewModel(this, null, f));
                    }
                },
                token);

            t.Wait(token);

            // TODO: Fix this
            //if (!string.IsNullOrWhiteSpace(this.SelectedPath))
            //{
            //    foreach (FolderViewModel root in this.RootFolders)
            //    {
            //        FolderViewModel folder = this.GetFolderByPath(root, this.SelectedPath.Split('\\').ToList());
            //        if (folder != null)
            //        {
            //            t.ContinueWith(
            //                t2 =>
            //                {
            //                    folder.IsExpanded = true;
            //                    folder.IsSelected = true;
            //                }, token);
            //            break;
            //        }
            //    }
            //}
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

            folder.IsExpanded = true;
            var subFolder = folder.SubFolders.FirstOrDefault(f => f.Name.Equals(path.First()));

            if (subFolder == null)
            {
                return null;
            }

            path.RemoveAt(0);
            return this.GetFolderByPath(subFolder, path);
        }


        public FolderBrowserViewModel(AdapterBase syncAdapter)
        {
            Pre.ThrowIfArgumentNull(syncAdapter, "syncAdapter");

            this.CloseWindowCommand = new DelegatedCommand(o => this.HandleClose(false));
            this.CancelCommand = new DelegatedCommand(o => this.HandleClose(false));
            this.OKCommand = new DelegatedCommand(o => this.HandleClose(true), this.CanOkCommandExecute);

            this.syncAdapter = syncAdapter;
        }

        private bool CanOkCommandExecute(object o)
        {
            return this.SelectedFolder != null && string.IsNullOrWhiteSpace(this.SelectedFolder.ErrorMessage);
        }

        private void HandleClose(bool dialogResult)
        {
            if (dialogResult)
            {
                //this.SetSelectedPath();
            }

            this.RequestClose?.Invoke(this, new RequestCloseEventArgs(dialogResult));
        }

        //private void SetSelectedPath()
        //{
        //    foreach (FolderViewModel folder in this.RootFolders)
        //    {
        //        FolderViewModel activeFolder = FindSelectedFolder(folder);
        //        if (activeFolder != null)
        //        {
        //            this.SelectedPath = activeFolder.GetPath();
        //            return;
        //        }
        //    }
        //}

        //private static FolderViewModel FindSelectedFolder(FolderViewModel folder)
        //{
        //    if (folder.IsSelected)
        //    {
        //        return folder;
        //    }

        //    foreach (FolderViewModel subFolder in folder.SubFolders)
        //    {
        //        var activeSubFolder = FindSelectedFolder(subFolder);
        //        if (activeSubFolder != null)
        //        {
        //            return activeSubFolder;
        //        }
        //    }

        //    return null;
        //}

        #region IRequestClose

        public event RequestCloseEventHandler RequestClose;

        public void WindowClosing(CancelEventArgs e)
        {
            if (this.MustClose)
            {
                // We are being forced to close, so don't show the confirmation message.
                e.Cancel = false;
                return;
            }
        }

        public bool MustClose { get; set; }

        #endregion IRequestClose
    }
}