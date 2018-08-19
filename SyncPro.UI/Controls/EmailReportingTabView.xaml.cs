using System.Windows;
using System.Windows.Controls;

namespace SyncPro.UI.Controls
{
    using SyncPro.UI.ViewModels;

    /// <summary>
    /// Interaction logic for EmailReportingTabView.xaml
    /// </summary>
    public partial class EmailReportingTabView
    {
        public EmailReportingTabView()
        {
            InitializeComponent();
        }

        private void OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is EmailReportingTabViewModel viewModel)
            {
                viewModel.SmtpPassword = ((PasswordBox) sender).SecurePassword;
            }
        }
    }
}
