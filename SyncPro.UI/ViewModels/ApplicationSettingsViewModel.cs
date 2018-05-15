namespace SyncPro.UI.ViewModels
{
    using System;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Windows.Input;

    using SyncPro.UI.Framework;
    using SyncPro.UI.Framework.MVVM;

    public class ApplicationSettingsViewModel : ViewModelBase, IRequestClose, ITabControlHostViewModel
    {
        public ICommand OKCommand { get; }

        public ICommand CancelCommand { get; }


        private ObservableCollection<TabPageViewModelBase> tabItems;

        public ObservableCollection<TabPageViewModelBase> TabItems =>
            this.tabItems ?? (this.tabItems = new ObservableCollection<TabPageViewModelBase>());

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private TabPageViewModelBase currentTabPage;

        public TabPageViewModelBase CurrentTabPage
        {
            get { return this.currentTabPage; }
            set { this.SetProperty(ref this.currentTabPage, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private EmailReportingTabViewModel emailReporting;

        public EmailReportingTabViewModel EmailReporting
        {
            get => this.emailReporting;
            set => this.SetProperty(ref this.emailReporting, value);
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private HelpTabViewModel helpPage;

        public HelpTabViewModel HelpPage
        {
            get { return this.helpPage; }
            set { this.SetProperty(ref this.helpPage, value); }
        }

        public ApplicationSettingsViewModel()
        {
            this.CancelCommand = new DelegatedCommand(o => this.HandleClose(false));
            this.OKCommand = new DelegatedCommand(o => this.HandleClose(true), this.CanOkCommandExecute);

            this.EmailReporting = new EmailReportingTabViewModel(this);
            this.HelpPage = new HelpTabViewModel(this);
            
            this.TabItems.Add(this.EmailReporting);
            this.TabItems.Add(this.HelpPage);

            this.CurrentTabPage = this.EmailReporting;
        }

        private bool CanOkCommandExecute(object o)
        {
            return true;
        }

        public void Save()
        {
            throw new NotImplementedException();
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