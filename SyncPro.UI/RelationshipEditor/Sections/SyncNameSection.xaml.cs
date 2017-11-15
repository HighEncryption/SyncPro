namespace SyncPro.UI.RelationshipEditor.Sections
{
    using System.Windows;

    /// <summary>
    /// Interaction logic for SyncNameSection.xaml
    /// </summary>
    public partial class SyncNameSection
    {
        public SyncNameSection()
        {
            this.InitializeComponent();
        }

        private void SyncNameSection_OnLoaded(object sender, RoutedEventArgs e)
        {
            SyncNamePageViewModel vm = this.DataContext as SyncNamePageViewModel;
            if (vm != null)
            {
                vm.ComputeDefaultName();
            }
        }
    }
}
