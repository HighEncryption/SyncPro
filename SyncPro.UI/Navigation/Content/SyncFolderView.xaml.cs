namespace SyncPro.UI.Navigation.Content
{
    using System.Linq;
    using System.Windows.Controls;

    using SyncPro.UI.Navigation.ViewModels;

    /// <summary>
    /// Interaction logic for SyncFolderView.xaml
    /// </summary>
    public partial class SyncFolderView
    {
        public SyncFolderView()
        {
            this.InitializeComponent();

        }

        private void SyncEntriesListViewSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var viewModel = this.DataContext as IFolderNodeViewModel;
            var listView = sender as ListView;

            if (viewModel == null || listView == null)
            {
                return;
            }

            viewModel.SelectedChildEntries = listView.SelectedItems.Cast<SyncEntryViewModel>().ToList();
        }
    }
}
