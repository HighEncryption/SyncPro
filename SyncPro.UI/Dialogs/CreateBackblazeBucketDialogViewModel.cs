namespace SyncPro.UI.Dialogs
{
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Windows.Input;

    using SyncPro.Adapters.BackblazeB2;
    using SyncPro.UI.Framework;
    using SyncPro.UI.Framework.MVVM;

    public class CreateBackblazeBucketDialogViewModel : ViewModelBase, IRequestClose
    {
        public ICommand OKCommand { get; }

        public ICommand CancelCommand { get; }

        public ICommand CloseWindowCommand { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string bucketName;

        public string BucketName
        {
            get { return this.bucketName; }
            set { this.SetProperty(ref this.bucketName, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string bucketType;

        public string BucketType
        {
            get { return this.bucketType; }
            set { this.SetProperty(ref this.bucketType, value); }
        }

        public CreateBackblazeBucketDialogViewModel()
        {
            this.CloseWindowCommand = new DelegatedCommand(o => this.HandleClose(false));
            this.CancelCommand = new DelegatedCommand(o => this.HandleClose(false));
            this.OKCommand = new DelegatedCommand(o => this.HandleClose(true), this.CanOkCommandExecute);

            // Create bucket type as private by default
            this.BucketType = Constants.BucketTypes.Private;
        }

        private bool CanOkCommandExecute(object o)
        {
            return !string.IsNullOrWhiteSpace(this.BucketName) &&
                   !string.IsNullOrWhiteSpace(this.BucketType);
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