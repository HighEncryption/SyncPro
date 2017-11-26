namespace SyncPro.UI.ViewModels
{
    using System.Diagnostics;

    using SyncPro.UI.Framework;

    public abstract class WizardPageViewModelBase : ViewModelBase
    {
        public RelationshipEditorViewModel EditorViewModel { get; }

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
                    if (this.EditorViewModel.CurrentWizardPage != this)
                    {
                        this.EditorViewModel.CurrentWizardPage = this;
                    }
                }
            }
        }

        protected WizardPageViewModelBase(RelationshipEditorViewModel editorViewModel)
            : base(true)
        {
            this.EditorViewModel = editorViewModel;
        }

        public abstract void LoadContext();

        public abstract void SaveContext();
    }
}