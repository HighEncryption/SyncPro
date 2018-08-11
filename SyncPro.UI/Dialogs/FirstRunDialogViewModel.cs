namespace SyncPro.UI.Dialogs
{
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Windows.Input;

    using SyncPro.UI.Framework;
    using SyncPro.UI.Framework.MVVM;

    public class FirstRunDialogViewModel : ViewModelBase, IRequestClose
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool acceptUsage;

        public bool AcceptUsage
        {
            get => this.acceptUsage;
            set => this.SetProperty(ref this.acceptUsage, value);
        }

        public ICommand OKCommand { get; }

        public ICommand CancelCommand { get; }

        public FirstRunDialogViewModel()
        {
            this.OKCommand = new DelegatedCommand(o => this.HandleClose(true), this.CanOkCommandExecute);
            this.CancelCommand = new DelegatedCommand(o => this.HandleClose(true));
        }

        private bool CanOkCommandExecute(object o)
        {
            return this.AcceptUsage;
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