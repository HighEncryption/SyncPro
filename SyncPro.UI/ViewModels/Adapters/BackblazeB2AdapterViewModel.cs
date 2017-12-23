namespace SyncPro.UI.ViewModels.Adapters
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Input;

    using SyncPro.Adapters;
    using SyncPro.Adapters.BackblazeB2;
    using SyncPro.Adapters.BackblazeB2.DataModel;
    using SyncPro.UI.Controls;
    using SyncPro.UI.Framework.MVVM;
    using SyncPro.UI.Navigation.ViewModels;

    public class BackblazeB2AdapterViewModel : SyncAdapterViewModel
    {
        public BackblazeB2AdapterViewModel(AdapterBase adapter)
            : base(adapter)
        {
            this.AddAccountInfoCommand = new DelegatedCommand(this.AddAccountInfo, o => this.CanAddAccountInfo);
            this.CreateBucketCommand = new DelegatedCommand(this.CreateBucket);

            this.CanAddAccountInfo = true;
            this.AccountInfoMessage = "Account information not set";

            this.UpdateSignInButton();

            this.Adapter.InitializationComplete += this.AdapterOnInitializationComplete;
        }

        public override string DisplayName => "Backblaze B2";

        public override string ShortDisplayName => "Backblaze B2";

        public override string LogoImage => "/SyncPro.UI;component/Resources/ProviderLogos/backblaze_b2.png";

        public BackblazeB2Adapter Adapter => (BackblazeB2Adapter)this.AdapterBase;

        public override string DestinationPath { get; set; }

        public ICommand AddAccountInfoCommand { get; }

        public ICommand CreateBucketCommand { get; }

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

        private ObservableCollection<Bucket> buckets;

        public ObservableCollection<Bucket> Buckets =>
            this.buckets ?? (this.buckets = new ObservableCollection<Bucket>());

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Bucket selectedBucket;

        public Bucket SelectedBucket
        {
            get { return this.selectedBucket; }
            set
            {
                if (this.SetProperty(ref this.selectedBucket, value))
                {
                    if (value == null)
                    {
                        this.BucketTypeMessage = null;
                        this.DestinationPath = null;
                    }
                    else
                    {
                        this.DestinationPath = value.BucketName;
                        string type = value.BucketType;
                        switch (type)
                        {
                            case Constants.BucketTypes.Public:
                                type = "Public";
                                break;
                            case Constants.BucketTypes.Private:
                                type = "Private";
                                break;
                        }

                        this.BucketTypeMessage = "Bucket type is " + type;
                    }
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string bucketTypeMessage;

        public string BucketTypeMessage
        {
            get { return this.bucketTypeMessage; }
            set { this.SetProperty(ref this.bucketTypeMessage, value); }
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

                this.Adapter.InitializeAsync().ConfigureAwait(false);
            }
        }

        private void AdapterOnInitializationComplete(object sender, EventArgs eventArgs)
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

            Task.Factory.StartNew(async () =>
            {
                IList<Bucket> result = await this.Adapter.GetBucketsAsync().ConfigureAwait(false);

                foreach (Bucket bucket in result)
                {
                    App.DispatcherInvoke(() => { this.Buckets.Add(bucket); });
                }
            });

            CommandManager.InvalidateRequerySuggested();
        }

        private void CreateBucket(object obj)
        {
            CreateBackblazeBucketDialogViewModel dialogViewModel = new CreateBackblazeBucketDialogViewModel();
            CreateBackblazeBucketDialog dialog = new CreateBackblazeBucketDialog
            {
                DataContext = dialogViewModel
            };

            bool? dialogResult = dialog.ShowDialog();

            if (dialogResult.HasValue && dialogResult.Value)
            {
                Task.Factory.StartNew(async () =>
                {
                    try
                    {
                        Bucket newBucket = await this.Adapter.CreateBucket(
                            dialogViewModel.BucketName,
                            dialogViewModel.BucketType);

                        App.DispatcherInvoke(() =>
                        {
                            this.Buckets.Add(newBucket);
                            this.SelectedBucket = newBucket;
                        });
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show(e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }).ConfigureAwait(false);
            }
        }

        private void UpdateSignInButton()
        {
            this.AddAccountInfoButtonText = 
                this.IsAccountInfoAdded ? "Change account information" : "Add account information";
        }
    }
}
