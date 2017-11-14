namespace SyncPro.UI.Navigation.Content
{
    using SyncPro.UI.ViewModels;

    /// <summary>
    /// Interaction logic for SyncRelationshipView.xaml
    /// </summary>
    public partial class SyncRelationshipView
    {
        public SyncRelationshipView()
        {
            this.InitializeComponent();

            this.DataContextChanged += async (sender, args) =>
            {
                SyncRelationshipViewModel viewModel = this.DataContext as SyncRelationshipViewModel;
                if (viewModel != null)
                {
                    await viewModel.CalculateRelationshipMetadataAsync();
                }
            };
        }
    }
}
