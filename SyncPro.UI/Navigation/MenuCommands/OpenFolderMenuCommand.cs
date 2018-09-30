namespace SyncPro.UI.Navigation.MenuCommands
{
    using System.Diagnostics;
    using System.IO;

    using SyncPro.Adapters.WindowsFileSystem;
    using SyncPro.UI.ViewModels.Adapters;

    public class OpenFolderMenuCommand : NavigationItemMenuCommand
    {
        private readonly ISyncTargetViewModel syncTargetViewModel;

        public OpenFolderMenuCommand(ISyncTargetViewModel syncTargetViewModel)
            : base(string.Empty, "/SyncPro.UI;component/Resources/Graphics/folder_open_16.png")
        {
            this.syncTargetViewModel = syncTargetViewModel;

            var adapter = this.syncTargetViewModel.AdapterBase as WindowsFileSystemAdapter;
            Pre.Assert(adapter != null, "adapter != null");

            this.Header = string.Format("OPEN '{0}'", Path.GetFileName(adapter.Config.RootDirectory));
        }

        protected override bool CanInvokeCommand(object obj)
        {
            return true;
        }

        protected override void InvokeCommand(object obj)
        {
            var adapter = this.syncTargetViewModel.AdapterBase as WindowsFileSystemAdapter;
            Pre.Assert(adapter != null, "adapter != null");

            Process.Start(adapter.Config.RootDirectory);
        }
    }
}