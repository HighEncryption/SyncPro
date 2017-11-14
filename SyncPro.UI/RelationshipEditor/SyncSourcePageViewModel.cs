namespace SyncPro.UI.RelationshipEditor
{
    using System.Linq;

    using JsonLog;

    using SyncPro.Adapters;
    using SyncPro.UI.ViewModels;
    using SyncPro.UI.ViewModels.Adapters;

    public class SyncSourcePageViewModel : SyncAdaptersPageViewModel
    {
        public SyncSourcePageViewModel(RelationshipEditorViewModel editorViewModel)
            : base(editorViewModel, true)
        {
            this.ErrorsChanged += (sender, args) =>
            {
                Logger.Warning("SyncSourcePageViewModel: HasErrors is now " + this.HasErrors);
            };
        }

        public override bool IsFirstPage => true;

        public override string TabItemImageSource => "/SyncPro.UI;component/Resources/Graphics/sort_up_20.png";

        public override void LoadContext()
        {
            AdapterBase selectedAdapter = null;

            if (this.EditorViewModel.Relationship.SyncSourceAdapter != null)
            {
                selectedAdapter = this.EditorViewModel.Relationship.SyncSourceAdapter.AdapterBase;
            }

            this.SelectedSyncAdapter = selectedAdapter == null
                ? this.SyncAdapters.First()
                : this.SyncAdapters.FirstOrDefault(vm => vm.GetAdapterType() == selectedAdapter.GetType());

            foreach (ISyncTargetViewModel viewModel in this.SyncAdapters)
            {
                viewModel.LoadContext();
            }
        }

        public override void SaveContext()
        {
            this.SelectedSyncAdapter.SaveContext();

            this.EditorViewModel.Relationship.SyncSourceAdapter = this.SelectedSyncAdapter;
        }

        public override string NavTitle => "Source";

        public override string PageTitle => "Synchronization Source";

        public override string PageSubText
            => "Select the place where files and folders will be copied from. This can be a folder on your location computer, or from an online service provider such as Microsoft OneDrive, Google Drive™, or Box.";
    }
}