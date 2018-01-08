namespace SyncPro.UI.Dialogs
{
    using System.Windows;
    using System.Windows.Controls;

    using SyncPro.UI.Navigation.ViewModels;

    /// <summary>
    /// Interaction logic for BackblazeCredentialDialog.xaml
    /// </summary>
    public partial class BackblazeCredentialDialog
    {
        public BackblazeCredentialDialog()
        {
            this.InitializeComponent();
        }

        private void OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            BackblazeCredentialDialogViewModel viewModel =
                this.DataContext as BackblazeCredentialDialogViewModel;

            if (viewModel != null)
            {
                viewModel.ApplicationKey = ((PasswordBox) sender).SecurePassword;
            }
        }
    }
}
