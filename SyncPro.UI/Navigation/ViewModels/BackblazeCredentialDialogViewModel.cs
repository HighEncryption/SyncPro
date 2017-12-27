namespace SyncPro.UI.Navigation.ViewModels
{
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Security;
    using System.Windows.Input;

    using SyncPro.UI.Framework;
    using SyncPro.UI.Framework.MVVM;

    public class BackblazeCredentialDialogViewModel : ViewModelBase, IRequestClose
    {
        public ICommand OKCommand { get; }

        public ICommand CancelCommand { get; }

        public ICommand CloseWindowCommand { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string accountId;

        public string AccountId
        {
            get { return this.accountId; }
            set { this.SetProperty(ref this.accountId, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private SecureString applicationKey;

        public SecureString ApplicationKey
        {
            get { return this.applicationKey; }
            set { this.SetProperty(ref this.applicationKey, value); }
        }

        public BackblazeCredentialDialogViewModel()
        {
            this.CloseWindowCommand = new DelegatedCommand(o => this.HandleClose(false));
            this.CancelCommand = new DelegatedCommand(o => this.HandleClose(false));
            this.OKCommand = new DelegatedCommand(o => this.HandleClose(true), this.CanOkCommandExecute);
        }

        private bool CanOkCommandExecute(object o)
        {
            return !string.IsNullOrWhiteSpace(this.AccountId) &&
                this.ApplicationKey?.Length > 0;
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