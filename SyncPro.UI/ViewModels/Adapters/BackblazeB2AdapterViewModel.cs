namespace SyncPro.UI.ViewModels.Adapters
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using System.Windows.Input;

    using SyncPro.Adapters;
    using SyncPro.Adapters.BackblazeB2;
    using SyncPro.UI.Controls;
    using SyncPro.UI.Framework.MVVM;
    using SyncPro.UI.Navigation.ViewModels;

    public class BackblazeB2AdapterViewModel : SyncAdapterViewModel
    {
        public BackblazeB2AdapterViewModel(AdapterBase adapter)
            : base(adapter)
        {
            this.AddAccountInfoCommand = new DelegatedCommand(this.AddAccountInfo, o => this.CanAddAccountInfo);
            this.BrowsePathCommand = new DelegatedCommand(this.BrowsePath);

            this.CanAddAccountInfo = true;
            this.AccountInfoMessage = "Account information not set";

            this.UpdateSignInButton();
        }

        public override string DisplayName => "Backblaze B2";

        public override string ShortDisplayName => "Backblaze B2";

        public override string LogoImage => "/SyncPro.UI;component/Resources/ProviderLogos/backblaze_b2.png";

        public BackblazeB2Adapter Adapter => (BackblazeB2Adapter)this.AdapterBase;

        public override string DestinationPath { get; set; }

        public ICommand AddAccountInfoCommand { get; }

        public ICommand BrowsePathCommand { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string addAccountInfoButtonText;

        public string AddAccountInfoButtonText
        {
            get { return this.addAccountInfoButtonText; }
            set { this.SetProperty(ref this.addAccountInfoButtonText, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool canAddAccountInfo;

        public bool CanAddAccountInfo
        {
            get { return this.canAddAccountInfo; }
            set { this.SetProperty(ref this.canAddAccountInfo, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string accountInfoMessage;

        public string AccountInfoMessage
        {
            get { return this.accountInfoMessage; }
            set { this.SetProperty(ref this.accountInfoMessage, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool isAccountInfoAdded;

        public bool IsAccountInfoAdded
        {
            get { return this.isAccountInfoAdded; }
            set { this.SetProperty(ref this.isAccountInfoAdded, value); }
        }

        public override void LoadContext()
        {
        }

        public override void SaveContext()
        {
        }

        public override Type GetAdapterType()
        {
            return typeof(BackblazeB2Adapter);
        }

        public static BackblazeB2AdapterViewModel CreateFromRelationship(SyncRelationshipViewModel relationship, bool isSourceAdapter)
        {
            ISyncTargetViewModel existingAdapter =
                isSourceAdapter ? relationship.SyncSourceAdapter : relationship.SyncDestinationAdapter;
            BackblazeB2AdapterViewModel model = existingAdapter as BackblazeB2AdapterViewModel;
            if (model != null)
            {
                return model;
            }

            return relationship.CreateAdapterViewModel<BackblazeB2AdapterViewModel>();
        }

        private void AddAccountInfo(object obj)
        {
            BackblazeCredentialDialogViewModel dialogViewModel = new BackblazeCredentialDialogViewModel();
            BackblazeCredentialDialog dialog = new BackblazeCredentialDialog
            {
                DataContext = dialogViewModel
            };

            bool? dialogResult = dialog.ShowDialog();

            if (dialogResult.HasValue && dialogResult.Value)
            {
                BackblazeB2AdapterConfiguration adapterConfiguration = 
                    (BackblazeB2AdapterConfiguration)this.Adapter.Configuration;

                adapterConfiguration.AccountId = dialogViewModel.AccountId;
                adapterConfiguration.ApplicationKey = dialogViewModel.ApplicationKey;

                this.Adapter.InitializeAsync()
                    .ContinueWith(this.AdapterInitializationComplete)
                    .ConfigureAwait(false);
            }
        }

        private void AdapterInitializationComplete(Task obj)
        {
            if (!this.Adapter.IsInitialized)
            {
                return;
            }

            this.AccountInfoMessage = string.Format(
                "Using Backblaze account {0}",
                this.Adapter.AccountId);

            this.IsAccountInfoAdded = true;
            this.CanAddAccountInfo = true;

            this.UpdateSignInButton();

            CommandManager.InvalidateRequerySuggested();
        }

        private void BrowsePath(object obj)
        {

        }

        private void UpdateSignInButton()
        {
            this.AddAccountInfoButtonText = 
                this.IsAccountInfoAdded ? "Change account information" : "Add account information";
        }
    }
}
