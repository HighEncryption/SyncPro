namespace SyncPro.UI.RelationshipEditor
{
    using System.Collections.ObjectModel;
    using System.Diagnostics;

    using SyncPro.Adapters;
    using SyncPro.UI.Framework.Validation.Rules;
    using SyncPro.UI.ViewModels;
    using SyncPro.UI.ViewModels.Adapters;

    public abstract class SyncAdaptersPageViewModel : WizardPageViewModelBase
    {
        protected SyncAdaptersPageViewModel(RelationshipEditorViewModel editorViewModel, bool isSourceAdapter)
            : base(editorViewModel)
        {
            //this.SyncAdapters.Add(AmazonS3SyncTargetViewModel.CreateFromRelationship(this.EditorViewModel.SyncRelationship, isSourceAdapter));
            //this.SyncAdapters.Add(AzureStorageSyncTargetViewModel.CreateFromRelationship(this.EditorViewModel.SyncRelationship, isSourceAdapter));
            this.SyncAdapters.Add(WindowsFileSystemAdapterViewModel.CreateFromRelationship(this.EditorViewModel.Relationship, isSourceAdapter));
            this.SyncAdapters.Add(OneDriveAdapterViewModel.CreateFromRelationship(this.EditorViewModel.Relationship, isSourceAdapter));
            this.SyncAdapters.Add(GoogleDriveAdapterViewModel.CreateFromRelationship(this.EditorViewModel.Relationship, isSourceAdapter));
            this.SyncAdapters.Add(BackblazeB2AdapterViewModel.CreateFromRelationship(this.EditorViewModel.Relationship, isSourceAdapter));
        }

        private ObservableCollection<ISyncTargetViewModel> syncAdapters;

        public ObservableCollection<ISyncTargetViewModel> SyncAdapters => 
            this.syncAdapters ?? (this.syncAdapters = new ObservableCollection<ISyncTargetViewModel>());

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private ISyncTargetViewModel selectedSyncAdapter;

        [ChildElementValidationRule]
        public ISyncTargetViewModel SelectedSyncAdapter
        {
            get { return this.selectedSyncAdapter; }
            set { this.SetProperty(ref this.selectedSyncAdapter, value); }
        }

        protected AdapterBase GetSelectedUnderlyingAdapterBase()
        {
            return this.SelectedSyncAdapter.AdapterBase;
        }
    }
}