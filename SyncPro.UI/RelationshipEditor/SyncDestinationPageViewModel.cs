namespace SyncPro.UI.RelationshipEditor
{
    using System.Linq;

    using SyncPro.Adapters;
    using SyncPro.UI.ViewModels;
    using SyncPro.UI.ViewModels.Adapters;

    public class SyncDestinationPageViewModel : SyncAdaptersPageViewModel
    {
        public SyncDestinationPageViewModel(RelationshipEditorViewModel editorViewModel)
            : base(editorViewModel, false)
        {
        }

        public override string TabItemImageSource => "/SyncPro.UI;component/Resources/Graphics/sort_down_20.png";

        public override void LoadContext()
        {
            foreach (ISyncTargetViewModel viewModel in this.SyncAdapters)
            {
                viewModel.LoadContext();
            }

            //AdapterBase selectedAdapter =
            //    this.EditorViewModel.Relationship.Model.Adapters.FirstOrDefault(a => !a.Configuration.IsOriginator);
            AdapterBase selectedAdapter = null;

            if (this.EditorViewModel.Relationship.SyncDestinationAdapter != null)
            {
                selectedAdapter = this.EditorViewModel.Relationship.SyncDestinationAdapter.AdapterBase;
            }

            this.SelectedSyncAdapter = selectedAdapter == null
                ? this.SyncAdapters.First()
                : this.SyncAdapters.FirstOrDefault(vm => vm.GetAdapterType() == selectedAdapter.GetType());
        }

        public override void SaveContext()
        {
            this.SelectedSyncAdapter.SaveContext();

            this.EditorViewModel.Relationship.SyncDestinationAdapter = this.SelectedSyncAdapter;
        }

        public override string NavTitle => "Destination";

        public override string PageTitle => "Synchronization Destination";

        public override string PageSubText
            => "Select the place where files and folders will be copied to. This can be a folder on your location computer, or from an online service provider such as Microsoft OneDrive, Google Drive™, or Box.";
    }
}