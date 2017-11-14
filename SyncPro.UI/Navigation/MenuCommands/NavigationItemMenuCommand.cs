namespace SyncPro.UI.Navigation.MenuCommands
{
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Windows.Input;

    using SyncPro.UI.Framework;
    using SyncPro.UI.Framework.MVVM;

    public abstract class NavigationItemMenuCommand : ViewModelBase
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string header;

        public string Header
        {
            get { return this.header; }
            set { this.SetProperty(ref this.header, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string iconImageSource;

        public string IconImageSource
        {
            get { return this.iconImageSource; }
            set { this.SetProperty(ref this.iconImageSource, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string toolTip;

        public string ToolTip
        {
            get { return this.toolTip; }
            set { this.SetProperty(ref this.toolTip, value); }
        }

        public ICommand Command { get; }

        protected NavigationItemMenuCommand(string header, string imageSource)
        {
            this.Header = header;
            this.IconImageSource = imageSource;
            this.Command = new DelegatedCommand(this.InvokeCommand, this.CanInvokeCommand);
        }

        protected abstract bool CanInvokeCommand(object obj);

        protected abstract void InvokeCommand(object obj);
    }
}