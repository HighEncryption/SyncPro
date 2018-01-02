namespace SyncPro.UI.ViewModels
{
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Security.Cryptography.X509Certificates;
    using System.Windows.Input;

    using SyncPro.UI.Framework;
    using SyncPro.UI.Framework.MVVM;

    public enum EncryptionType
    {
        None,
        Encrypt,
        Decrypt
    }

    public class EncryptionSettingsDialogViewModel : ViewModelBase, IRequestClose
    {
        public ICommand OKCommand { get; }

        public ICommand CancelCommand { get; }

        public ICommand CloseWindowCommand { get; }

        public ICommand OpenCertificateCommand { get; }

        public EncryptionSettingsDialogViewModel()
        {
            this.OKCommand = new DelegatedCommand(o => this.HandleClose(true), this.CanOkCommandExecute);
            this.CancelCommand = new DelegatedCommand(o => this.HandleClose(false));
            this.CloseWindowCommand = new DelegatedCommand(o => this.HandleClose(false));
            this.OpenCertificateCommand = new DelegatedCommand(
                o => this.OpenCertificate(),
                o => this.LoadExistingCertificate);

            this.CreateNewCertificate = true;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool isEncryptionEnabled;

        public bool IsEncryptionEnabled
        {
            get { return this.isEncryptionEnabled; }
            set { this.SetProperty(ref this.isEncryptionEnabled, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool createNewCertificate;

        public bool CreateNewCertificate
        {
            get { return this.createNewCertificate; }
            set { this.SetProperty(ref this.createNewCertificate, value); }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool loadExistingCertificate;

        public bool LoadExistingCertificate
        {
            get { return this.loadExistingCertificate; }
            set { this.SetProperty(ref this.loadExistingCertificate, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool existingCertificateIsValid;

        public bool ExistingCertificateIsValid
        {
            get { return this.existingCertificateIsValid; }
            set { this.SetProperty(ref this.existingCertificateIsValid, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private X509Certificate2 existingCertificate;

        public X509Certificate2 ExistingCertificate
        {
            get { return this.existingCertificate; }
            set { this.SetProperty(ref this.existingCertificate, value); }
        }

        private void OpenCertificate()
        {
        }

        private bool CanOkCommandExecute(object o)
        {
            return !this.IsEncryptionEnabled ||
                   (this.IsEncryptionEnabled && this.CreateNewCertificate) ||
                   (this.IsEncryptionEnabled && this.LoadExistingCertificate && this.ExistingCertificateIsValid);
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
