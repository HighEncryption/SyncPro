namespace SyncPro.UI.Dialogs
{
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Windows.Forms;
    using System.Windows.Input;

    using SyncPro.UI.Framework;
    using SyncPro.UI.Framework.MVVM;

    public class RestoreItemsDialogViewModel : ViewModelBase, IRequestClose
    {
        public ICommand OKCommand { get; }

        public ICommand CancelCommand { get; }

        public ICommand CloseWindowCommand { get; }

        public ICommand RestoreBrowseCommand { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string dialogHeader;

        public string DialogHeader
        {
            get { return this.dialogHeader; }
            set { this.SetProperty(ref this.dialogHeader, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string dialogDescription;

        public string DialogDescription
        {
            get { return this.dialogDescription; }
            set { this.SetProperty(ref this.dialogDescription, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool restoreToSource;

        public bool RestoreToSource
        {
            get { return this.restoreToSource; }
            set { this.SetProperty(ref this.restoreToSource, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool restoreToNewLocation;

        public bool RestoreToNewLocation
        {
            get { return this.restoreToNewLocation; }
            set { this.SetProperty(ref this.restoreToNewLocation, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string restoreBrowsePath;

        public string RestoreBrowsePath
        {
            get { return this.restoreBrowsePath; }
            set { this.SetProperty(ref this.restoreBrowsePath, value); }
        }

        public RestoreItemsDialogViewModel()
        {
            this.CloseWindowCommand = new DelegatedCommand(o => this.HandleClose(false));
            this.CancelCommand = new DelegatedCommand(o => this.HandleClose(false));
            this.OKCommand = new DelegatedCommand(o => this.HandleClose(true), this.CanOkCommandExecute);

            this.RestoreBrowseCommand = new DelegatedCommand(this.BrowsePath);
        }

        private bool CanOkCommandExecute(object o)
        {
            return this.RestoreToSource || !string.IsNullOrWhiteSpace(this.RestoreBrowsePath);
        }

        private void BrowsePath(object obj)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                if (!string.IsNullOrWhiteSpace(this.RestoreBrowsePath))
                {
                    dialog.SelectedPath = this.RestoreBrowsePath;
                }

                dialog.Description = "Select the folder where items will be restored to.";
                System.Windows.Forms.DialogResult result = dialog.ShowDialog();

                if (result == DialogResult.OK)
                {
                    this.RestoreBrowsePath = dialog.SelectedPath;
                }
            }
        }

        private void HandleClose(bool dialogResult)
        {
            this.RequestClose?.Invoke(this, new RequestCloseEventArgs(dialogResult));
        }

        #region IRequestClose

        public event RequestCloseEventHandler RequestClose;

        public void WindowClosing(CancelEventArgs e)
        {
            if (this.MustClose)
            {
                // We are being forced to close, so don't show the confirmation message.
                e.Cancel = false;
            }
        }

        public bool MustClose { get; set; }

        #endregion IRequestClose
    }
}
