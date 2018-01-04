using System.Windows;

namespace SyncPro.UI.Navigation.Content
{
    /// <summary>
    /// Interaction logic for SyncJobHistoryView.xaml
    /// </summary>
    public partial class SyncJobHistoryView
    {
        public SyncJobHistoryView()
        {
            this.InitializeComponent();
        }

        public static readonly DependencyProperty NavigationNodeViewModelProperty = DependencyProperty.Register(
            "NavigationNodeViewModel", 
            typeof (NavigationNodeViewModel), 
            typeof (SyncJobHistoryView),
            new PropertyMetadata(default(NavigationNodeViewModel)));

        public NavigationNodeViewModel NavigationNodeViewModel
        {
            get { return (NavigationNodeViewModel) this.GetValue(NavigationNodeViewModelProperty); }
            set { this.SetValue(NavigationNodeViewModelProperty, value); }
        }
    }
}
