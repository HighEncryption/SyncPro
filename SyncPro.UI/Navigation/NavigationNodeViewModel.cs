namespace SyncPro.UI.Navigation
{
    using System.Collections.ObjectModel;
    using System.Diagnostics;

    using SyncPro.UI.Framework;
    using SyncPro.UI.Navigation.MenuCommands;

    public abstract class NavigationNodeViewModel : ViewModelBase
    {
        private ObservableCollection<NavigationNodeViewModel> children;

        public ObservableCollection<NavigationNodeViewModel> Children
            => this.children ?? (this.children = new ObservableCollection<NavigationNodeViewModel>());

        private ObservableCollection<NavigationItemMenuCommand> menuCommands;

        public ObservableCollection<NavigationItemMenuCommand> MenuCommands
            => this.menuCommands ?? (this.menuCommands = new ObservableCollection<NavigationItemMenuCommand>());

        private NavigationItemMenuCommand closePanelCommand;

        public NavigationItemMenuCommand ClosePanelCommand
        {
            get { return this.closePanelCommand; }
            set { this.SetProperty(ref this.closePanelCommand, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool isExpanded;

        // Gets a value indicating whether this sync relationship has been Expanded (some properties cannot be edited once Expanded).
        public bool IsExpanded
        {
            get { return this.isExpanded; }
            set
            {
                if (this.SetProperty(ref this.isExpanded, value) && value)
                {
                    // Expand up the tree
                    if (this.Parent != null)
                    {
                        this.Parent.IsExpanded = true;
                    }

                    this.LoadChildrenInternal();
                }
            }
        }

        private void LoadChildrenInternal()
        {
            // If this is a lazy loaded node, start loaded
            if (this.lazyLoadPlaceholder == null || this.isChildLoadingStarted)
            {
                return;
            }

            bool loadChildren = false;

            lock (this.childLoadLock)
            {
                if (!this.isChildLoadingStarted)
                {
                    this.isChildLoadingStarted = true;
                    loadChildren = true;
                }
            }

            if (loadChildren)
            {
                this.Children.Clear();
                this.LoadChildren();
            }
        }

        private volatile object childLoadLock = new object();

        private bool isChildLoadingStarted;

        protected virtual void LoadChildren()
        {
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool isSelected;

        // Gets a value indicating whether this sync relationship has been Selected (some properties cannot be edited once Selected).
        public bool IsSelected
        {
            get { return this.isSelected; }
            set
            {
                if (this.SetProperty(ref this.isSelected, value) && value)
                {
                    App.Current.MainWindowsViewModel.SelectedNavigationItem = this;

                    if (this.Parent != null)
                    {
                        this.Parent.IsExpanded = true;
                    }

                    this.LoadChildrenInternal();
                    this.OnIsSelected();
                }
            }
        }

        protected virtual void OnIsSelected()
        {
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool isExpanderVisible;

        // Gets a value indicating whether this sync relationship has been ExpanderVisible (some properties cannot be edited once ExpanderVisible).
        public bool IsExpanderVisible
        {
            get { return this.isExpanderVisible; }
            set { this.SetProperty(ref this.isExpanderVisible, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string name;

        public string Name
        {
            get { return this.name; }
            set { this.SetProperty(ref this.name, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string iconImageSource;

        public string IconImageSource
        {
            get { return this.iconImageSource; }
            set { this.SetProperty(ref this.iconImageSource, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool showStatusIcon;

        public bool ShowStatusIcon
        {
            get { return this.showStatusIcon; }
            set { this.SetProperty(ref this.showStatusIcon, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool showProgress;

        public bool ShowProgress
        {
            get { return this.showProgress; }
            set { this.SetProperty(ref this.showProgress, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool progressIsIndeterminate;

        public bool ProgressIsIndeterminate
        {
            get { return this.progressIsIndeterminate; }
            set { this.SetProperty(ref this.progressIsIndeterminate, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool progressValue;

        public bool ProgressValue
        {
            get { return this.progressValue; }
            set { this.SetProperty(ref this.progressValue, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string statusIconImageSource;

        public string StatusIconImageSource
        {
            get { return this.statusIconImageSource; }
            set { this.SetProperty(ref this.statusIconImageSource, value); }
        }

        public NavigationNodeViewModel Parent { get; }

        public ViewModelBase Item { get; }

        private readonly NavigationNodeViewModel lazyLoadPlaceholder;

        protected NavigationNodeViewModel(NavigationNodeViewModel parent, ViewModelBase item)
            : this(parent, item, null)
        {
        }

        protected NavigationNodeViewModel(NavigationNodeViewModel parent, ViewModelBase item, NavigationNodeViewModel lazyLoadPlaceholder)
        {
            this.Parent = parent;
            this.Item = item;
            this.lazyLoadPlaceholder = lazyLoadPlaceholder;

            if (lazyLoadPlaceholder != null)
            {
                // A placeholder node was provided by the caller, so this is a lazy loaded node
                this.Children.Add(lazyLoadPlaceholder);
            }

            this.IsExpanderVisible = true;
        }
    }
}