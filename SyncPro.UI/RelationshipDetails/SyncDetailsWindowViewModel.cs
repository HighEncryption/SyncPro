namespace SyncPro.UI.RelationshipDetails
{
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Windows.Input;

    using SyncPro.UI.Framework;
    using SyncPro.UI.Framework.MVVM;
    using SyncPro.UI.ViewModels;

    public class SyncDetailsWindowViewModel : ViewModelBase, IRequestClose
    {
        public ICommand CloseWindowCommand { get; private set; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private SyncRelationshipViewModel syncRelationship;

        public SyncRelationshipViewModel SyncRelationship
        {
            get { return this.syncRelationship; }
            set { this.SetProperty(ref this.syncRelationship, value); }
        }

        public SyncDetailsWindowViewModel()
        {
            this.CloseWindowCommand = new DelegatedCommand(this.CloseWindow);
        }

        private void CloseWindow(object obj)
        {
            this.RequestClose?.Invoke(this, new RequestCloseEventArgs());
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

        #endregion
    }
}