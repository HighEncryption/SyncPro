namespace SyncPro.UI.ViewModels
{
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Input;

    using SyncPro.Tracing;
    using SyncPro.UI.Framework;
    using SyncPro.UI.Framework.MVVM;
    using SyncPro.UI.Framework.Validation.Rules;
    using SyncPro.UI.Navigation.ViewModels;
    using SyncPro.UI.RelationshipEditor;

    public class RelationshipEditorViewModel : ViewModelBase, IRequestClose
    {
        public ICommand MovePreviousCommand { get; }

        public ICommand MoveNextCommand { get; }

        public ICommand OKCommand { get; }

        public ICommand CancelCommand { get; }

        public ICommand CloseWindowCommand { get; }

        public bool IsEditMode { get; }

        public bool IsCreateMode => !this.IsEditMode;

        [ChildElementValidationRule]
        public SyncSourcePageViewModel SyncSourcePageViewModel { get; }

        [ChildElementValidationRule]
        public SyncDestinationPageViewModel SyncDestinationPageViewModel { get; }

        [ChildElementValidationRule]
        public SyncFilteringPageViewModel SyncFilteringPageViewModel { get; }

        [ChildElementValidationRule]
        public SyncPerformancePageViewModel SyncPerformancePageViewModel { get; }

        [ChildElementValidationRule]
        public SyncTriggeringPageViewModel SyncTriggeringPageViewModel { get; }

        [ChildElementValidationRule]
        public SyncNamePageViewModel SyncNamePageViewModel { get; }


        private ObservableCollection<WizardPageViewModelBase> wizardPages;

        public ObservableCollection<WizardPageViewModelBase> WizardPages => 
            this.wizardPages ?? (this.wizardPages = new ObservableCollection<WizardPageViewModelBase>());

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private WizardPageViewModelBase currentWizardPage;

        public WizardPageViewModelBase CurrentWizardPage
        {
            get { return this.currentWizardPage; }
            set
            {
                var oldPage = this.currentWizardPage;
                if (this.SetProperty(ref this.currentWizardPage, value))
                {
                    if (oldPage != null)
                    {
                        oldPage.IsActive = false;
                    }

                    if (value != null)
                    {
                        value.IsActive = true;
                    }
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool hasUnsavedChanges;

        public bool HasUnsavedChanges
        {
            get { return this.hasUnsavedChanges; }
            set { this.SetProperty(ref this.hasUnsavedChanges, value); }
        }

        public SyncRelationshipViewModel Relationship { get; }

        public RelationshipEditorViewModel(SyncRelationshipViewModel relationship, bool isEditMode)
            : base(true)
        {
            this.Relationship = relationship;
            this.IsEditMode = isEditMode;

            this.MovePreviousCommand = new DelegatedCommand(this.MovePrevious, this.CanMovePrevious);
            this.MoveNextCommand = new DelegatedCommand(this.MoveNext, this.CanMoveNext);
            this.OKCommand = new DelegatedCommand(this.OK);
            this.CancelCommand = new DelegatedCommand(this.Cancel);
            this.CloseWindowCommand = new DelegatedCommand(this.CloseWindow);

            this.SyncSourcePageViewModel = new SyncSourcePageViewModel(this);
            this.WizardPages.Add(this.SyncSourcePageViewModel);

            this.SyncDestinationPageViewModel = new SyncDestinationPageViewModel(this);
            this.WizardPages.Add(this.SyncDestinationPageViewModel);

            this.SyncFilteringPageViewModel = new SyncFilteringPageViewModel(this);
            this.WizardPages.Add(this.SyncFilteringPageViewModel);

            this.SyncPerformancePageViewModel = new SyncPerformancePageViewModel(this);
            this.WizardPages.Add(this.SyncPerformancePageViewModel);

            this.SyncTriggeringPageViewModel = new SyncTriggeringPageViewModel(this);
            this.WizardPages.Add(this.SyncTriggeringPageViewModel);

            this.SyncNamePageViewModel = new SyncNamePageViewModel(this);
            this.WizardPages.Add(this.SyncNamePageViewModel);

            this.ErrorsChanged += (sender, args) =>
            {
                Logger.Warning("RelationshipEditor: HasErrors is now " + this.HasErrors);
            };

            foreach (WizardPageViewModelBase wizardPage in this.WizardPages)
            {
                wizardPage.LoadContext();
                wizardPage.LoadingComplete = true;
            }

            this.CurrentWizardPage = this.WizardPages.First();
        }

        private void CloseWindow(object obj)
        {
            RequestCloseEventHandler onRequestClose = this.RequestClose;
            onRequestClose?.Invoke(this, new RequestCloseEventArgs());
        }

        private void OK(object obj)
        {
            this.Revalidate();

            if (this.HasErrors)
            {
                return;
            }

            this.SaveOnExit = true;

            this.CloseWindowCommand.Execute(null);
        }

        private void Cancel(object obj)
        {
            this.SaveOnExit = false;
            this.CloseWindowCommand.Execute(null);
        }

        private bool CanMoveNext(object obj)
        {
            return this.WizardPages.IndexOf(this.CurrentWizardPage) < this.WizardPages.Count - 1;
        }

        private void MoveNext(object obj)
        {
            int currentPageIndex = this.WizardPages.IndexOf(this.CurrentWizardPage);
            this.CurrentWizardPage = this.WizardPages[currentPageIndex + 1];
        }

        private bool CanMovePrevious(object obj)
        {
            return this.WizardPages.IndexOf(this.CurrentWizardPage) > 0;
        }

        private void MovePrevious(object obj)
        {
            int currentPageIndex = this.WizardPages.IndexOf(this.CurrentWizardPage);
            this.CurrentWizardPage = this.WizardPages[currentPageIndex - 1];
        }

        public async Task CommitRelationshipAsync()
        {
            // Tell all of the wizard pages to write their viewmodel state to their respective contexts. This
            // is necessary before calling SaveAsync below since SaveAsync will rely on data in the context
            // objects to configure the relationship.
            foreach (WizardPageViewModelBase wizardPage in this.WizardPages)
            {
                wizardPage.SaveContext();
            }

            await this.Relationship.SaveAsync(true).ConfigureAwait(false);

            this.Relationship.LoadContext();
        }

        public bool SaveOnExit { get; set; }

        #region IRequestClose

        public event RequestCloseEventHandler RequestClose;

        public void WindowClosing(CancelEventArgs e)
        {
            if (this.MustClose)
            {
                // We are being forced to close, so don't show the confirmation message.
                e.Cancel = false;
                return;
            }

            if (this.HasUnsavedChanges)
            {
                MessageBoxResult result = MessageBox.Show(
                    "Close window?",
                    "SyncPro",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No);

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }
            }

            // If we are actually creating the relationship, create it an return.
            if (this.SaveOnExit)
            {
                // TODO: Ensure that this happens on a background thread?
                Task.Run(async () =>
                {
                    await this.CommitRelationshipAsync().ConfigureAwait(false);
                });

                if (this.IsCreateMode)
                {
                    App.Current.MainWindowsViewModel.SyncRelationships.Add(this.Relationship);
                    App.Current.MainWindowsViewModel.NavigationItems.Add(
                        new SyncRelationshipNodeViewModel(null, this.Relationship));
                }
            }
        }

        public bool MustClose { get; set; }

        #endregion IRequestClose
    }
}