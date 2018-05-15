namespace SyncPro.UI.RelationshipEditor
{
    using System.Diagnostics;

    using SyncPro.UI.ViewModels;

    public class SyncFilteringPageViewModel : RelationshipEditorPageViewModelBase
    {
        public SyncFilteringPageViewModel(RelationshipEditorViewModel editorViewModel)
            : base(editorViewModel)
        {
            this.SelectedScopeType = SyncScopeType.SourceToDestination;
        }

        public override string TabItemImageSource => "/SyncPro.UI;component/Resources/Graphics/filter_20.png";

        public override void LoadContext()
        {
            if (this.EditorViewModel.Relationship.Scope != SyncScopeType.Undefined)
            {
                this.SelectedScopeType = this.EditorViewModel.Relationship.Scope;
            }
        }

        public override void SaveContext()
        {
            this.EditorViewModel.Relationship.Scope = this.SelectedScopeType;
        }

        public override string NavTitle => "Filtering";

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private SyncScopeType selectedScopeType;

        public SyncScopeType SelectedScopeType
        {
            get { return this.selectedScopeType; }
            set
            {
                if (this.SetProperty(ref this.selectedScopeType, value))
                {
                    if (value == SyncScopeType.Bidirectional)
                    {
                        this.SyncScopeExplaination =
                            "New file, updates, and deletions will be copied from the source to the destination, as well as from the destination to the source. Both directories will be identicaly after synchronization.";
                    }
                    else if (value == SyncScopeType.SourceToDestination)
                    {
                        this.SyncScopeExplaination =
                            "New file, updates, and deletions will be copied from the source to the destination only. Changes made to the destination will not be copied to the source.";
                    }
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string syncScopeExplaination;

        public string SyncScopeExplaination
        {
            get { return this.syncScopeExplaination; }
            set { this.SetProperty(ref this.syncScopeExplaination, value); }
        }
    }
}