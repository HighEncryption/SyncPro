using System.Windows;

namespace SyncPro.UI.Navigation.Content
{
    /// <summary>
    /// Interaction logic for SyncRunHistoryView.xaml
    /// </summary>
    public partial class SyncRunHistoryView
    {
        public SyncRunHistoryView()
        {
            this.InitializeComponent();
        }

        public static readonly DependencyProperty NavigationNodeViewModelProperty = DependencyProperty.Register(
            "NavigationNodeViewModel", 
            typeof (NavigationNodeViewModel), 
            typeof (SyncRunHistoryView),
            new PropertyMetadata(default(NavigationNodeViewModel)));

        public NavigationNodeViewModel NavigationNodeViewModel
        {
            get { return (NavigationNodeViewModel) this.GetValue(NavigationNodeViewModelProperty); }
            set { this.SetValue(NavigationNodeViewModelProperty, value); }
        }
    }
}
