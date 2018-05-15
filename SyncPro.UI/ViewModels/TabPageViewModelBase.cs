namespace SyncPro.UI.ViewModels
{
    using System.Diagnostics;

    using SyncPro.UI.Framework;


    public abstract class RelationshipEditorPageViewModelBase : TabPageViewModelBase
    {
        public RelationshipEditorViewModel EditorViewModel => 
            (RelationshipEditorViewModel)this.TabControlHostView;

        protected RelationshipEditorPageViewModelBase(RelationshipEditorViewModel viewModel) 
            : base(viewModel)
        {
        }
    }

    public abstract class TabPageViewModelBase : ViewModelBase
    {
        public ITabControlHostViewModel TabControlHostView { get; }

        public abstract string NavTitle { get; }

        public virtual string PageTitle => this.NavTitle;

        public virtual string PageSubText => null;

        public virtual bool IsFirstPage => false;

        public virtual bool IsLastPage => false;

        public abstract string TabItemImageSource { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool isActive;

        public bool IsActive
        {
            get { return this.isActive; }
            set
            {
                if (this.SetProperty(ref this.isActive, value) && value)
                {
                    if (this.TabControlHostView.CurrentTabPage != this)
                    {
                        this.TabControlHostView.CurrentTabPage = this;
                    }
                }
            }
        }

        protected TabPageViewModelBase(ITabControlHostViewModel tabControlHostView)
            : base(true)
        {
            this.TabControlHostView = tabControlHostView;
        }

        public abstract void LoadContext();

        public abstract void SaveContext();
    }
}