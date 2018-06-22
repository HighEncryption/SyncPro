namespace SyncPro.UI.ViewModels.Adapters
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Windows.Input;
    using System.Windows.Interop;

    using SyncPro.Adapters.MicrosoftOneDrive;
    using SyncPro.Tracing;
    using SyncPro.UI.Dialogs;
    using SyncPro.UI.FolderBrowser;
    using SyncPro.UI.Framework.MVVM;

    public class OneDriveAdapterViewModel : SyncAdapterViewModel
    {
        public ICommand SignInCommand { get; }

        public ICommand BrowsePathCommand { get; }

        public override string DisplayName => "Microsoft OneDrive";

        public override string ShortDisplayName => "OneDrive";

        public override string LogoImage => "/SyncPro.UI;component/Resources/ProviderLogos/onedrive.png";

        public OneDriveAdapter Adapter => (OneDriveAdapter) this.AdapterBase;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string signInMessage;

        public string SignInMessage
        {
            get { return this.signInMessage; }
            set { this.SetProperty(ref this.signInMessage, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string signInButtonText;

        public string SignInButtonText
        {
            get { return this.signInButtonText; }
            set { this.SetProperty(ref this.signInButtonText, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool isSignedIn;

        public bool IsSignedIn
        {
            get { return this.isSignedIn; }
            set { this.SetProperty(ref this.isSignedIn, value); }
        }

        private bool canSignIn;

        public bool CanSignIn
        {
            get { return this.canSignIn; }
            set { this.SetProperty(ref this.canSignIn, value); }
        }

        public OneDriveAdapterViewModel(OneDriveAdapter adapter)
            : base(adapter)
        {
            this.SignInCommand = new DelegatedCommand(this.SignIn, o => this.CanSignIn);
            this.BrowsePathCommand = new DelegatedCommand(this.BrowsePath);

            this.CanSignIn = true;
            this.SignInMessage = "Not Signed In";

            adapter.InitializationComplete += (sender, args) => this.SetSignInStatus();

            if (adapter.IsInitialized)
            {
                this.SetSignInStatus();
            }

            // When creating a new relationship, the adapter will not be initialized, so update the sign-in 
            // button text directly.
            this.UpdateSignInButton();
        }

        public override string DestinationPath
        {
            get { return this.Adapter.Config.TargetPath; }
            set
            {
                this.SetPropertyDelegated("DestinationPath", this.Adapter.Config.TargetPath, value, () =>
                {
                    this.Adapter.Config.TargetPath = value;
                });
            }
        }

        public void UpdateSignInButton()
        {
            this.SignInButtonText = this.IsSignedIn ? "Sign out" : "Sign in to Microsoft OneDrive";
        }

        private FolderBrowserViewModel viewModel;

        private void BrowsePath(object obj)
        {
            FolderBrowserWindow window = new FolderBrowserWindow();
            if (this.viewModel == null)
            {
                this.viewModel = new FolderBrowserViewModel(this.AdapterBase)
                {
                    Message = this.Adapter.Configuration.IsOriginator ?
                        "Select the source folder" :
                        "Select the destination folder"
                };

                if (!string.IsNullOrWhiteSpace(this.DestinationPath))
                {
                    //this.viewModel.SelectedPath = this.DestinationPath;
                }
            }

            if (!this.viewModel.RootFolders.Any())
            {
                this.viewModel.LoadRootFolders();
            }

            window.DataContext = this.viewModel;

            if (!string.IsNullOrWhiteSpace(this.DestinationPath))
            {
                ThreadingHelper.StartBackgroundTask(
                    (s, t) =>
                    {
                        foreach (FolderViewModel root in this.viewModel.RootFolders)
                        {
                            FolderViewModel folder = this.GetFolderByPath(root,
                                this.DestinationPath.Split(new[] {this.AdapterBase.PathSeparator},
                                    StringSplitOptions.None).ToList());
                            if (folder != null)
                            {
                                folder.IsExpanded = true;
                                folder.IsSelected = true;
                                break;
                            }
                        }
                    });
            }

            bool? dialogResult = window.ShowDialog();

            if (dialogResult == true && this.viewModel.SelectedFolder != null)
            {
                this.DestinationPath = this.viewModel.SelectedFolder.GetPath();
            }
        }

        private FolderViewModel GetFolderByPath(FolderViewModel folder, IList<string> path)
        {
            if (!path.Any())
            {
                return folder;
            }

            if (!folder.AreSubFoldersLoaded)
            {
                return null;
            }

            var subFolder = folder.SubFolders.FirstOrDefault(f => f.Name.Equals(path.First()));

            if (subFolder == null)
            {
                return null;
            }

            path.RemoveAt(0);
            return this.GetFolderByPath(subFolder, path);
        }

        private void SignIn(object obj)
        {
            string logoutUri = OneDriveClient.GetDefaultLogoutUri();
            string authorizationUri = OneDriveClient.GetDefaultAuthorizationUri();

            AuthenticationResult authenticationResult = null;
            BrowserAuthenticationWindow authWindow = new BrowserAuthenticationWindow();
            authWindow.Browser.Navigated += (sender, args) =>
            {
                if (string.Equals(args.Uri.AbsolutePath, "/oauth20_logout.srf", StringComparison.OrdinalIgnoreCase))
                {
                    // The logout page has finished loading, so we can now load the login page.
                    authWindow.Browser.Navigate(authorizationUri);
                }

                // If the browser is directed to the redirect URI, the new URI will contain the access code that we can use to 
                // get a token for OneDrive.
                if (string.Equals(args.Uri.AbsolutePath, "ietf:wg:oauth:2.0:oob", StringComparison.OrdinalIgnoreCase))
                {
                    // We were directed back to the redirect URI. Extract the code from the query string
                    Dictionary<string, string> queryParametes = args.Uri.GetQueryParameters();

                    authenticationResult = new AuthenticationResult()
                    {
                        Code = queryParametes["code"]
                    };

                    // All done. Close the window.
                    authWindow.Close();
                }
            };

            authWindow.Closed += (sender, args) =>
            {
                if (authenticationResult == null)
                {
                    return;
                }

                this.CanSignIn = false;
                this.SignInMessage = "Working...";
                this.Adapter.SignIn(authenticationResult).ContinueWith(t => this.SetSignInStatus());
            };

            authWindow.Loaded += (sender, args) =>
            {
                authWindow.Browser.Navigate(logoutUri);
                NativeMethods.User32.SetForegroundWindow(new WindowInteropHelper(authWindow).Handle);
            };

            authWindow.ShowDialog();
        }

        private void SetSignInStatus()
        {
            var profile = this.Adapter.UserProfile;
            this.SignInMessage = !string.IsNullOrWhiteSpace(profile.Emails?.Account)
                ? string.Format("Signed in as {0} ({1})", profile.Name, profile.Emails.Account)
                : string.Format("Signed in as {0}", profile.Name);

            if (this.Adapter.IsFaulted)
            {
                if (this.Adapter.FaultInformation is OneDriveRefreshTokenExpiredFault)
                {
                    // Sign in will re-initialize the OneDriveClient, but it will not automatically save the newly
                    // generated token to the configuration since in the default case, the token was just read from
                    // the configuration. For this reason, we need to force a save of the new token.
                    Logger.Info("Saving refreshed token to configuration.");
                    this.Adapter.SaveCurrentTokenToConfiguration();
                }

                Logger.Debug("Sign in was successful. Clearing fault information.");
                this.Adapter.FaultInformation = null;
            }

            this.IsSignedIn = true;
            this.CanSignIn = true;
            this.UpdateSignInButton();
            CommandManager.InvalidateRequerySuggested();
        }

        public static OneDriveAdapterViewModel CreateFromRelationship(SyncRelationshipViewModel relationship, bool isSourceAdapter)
        {
            ISyncTargetViewModel existingAdapter = isSourceAdapter ? relationship.SyncSourceAdapter : relationship.SyncDestinationAdapter;
            OneDriveAdapterViewModel model = existingAdapter as OneDriveAdapterViewModel;
            if (model != null)
            {
                return model;
            }

            OneDriveAdapterViewModel adapterViewModel = relationship.CreateAdapterViewModel<OneDriveAdapterViewModel>();

            // If we are creating a new adapter view model (and adapter), set the IsOriginator property
            adapterViewModel.Adapter.Configuration.IsOriginator = isSourceAdapter;

            return adapterViewModel;
        }

        public override void LoadContext()
        {
            // 2017-06-21: Why are we calling LoadConfiguration on the adapter? That is for loading the adapter's config,
            // when what this method is for is loading loading state from the *adapter* into *this viewmodel*, so I am
            // not sure this is actually needed.
            // this.AdapterBase.LoadConfiguration();

            //if (this.Adapter.is)
        }

        public override void SaveContext()
        {
            // this.AdapterBase.SaveConfiguration();
        }

        public override Type GetAdapterType()
        {
            return typeof(OneDriveAdapter);
        }
    }

    public class PlaceholderAdapterViewModel : SyncAdapterViewModel
    {
        public PlaceholderAdapterViewModel() 
            : base(null)
        {
        }

        public override void LoadContext()
        {
        }

        public override void SaveContext()
        {
        }

        public override Type GetAdapterType()
        {
            return null;
        }

        public override string DisplayName => "Select a provider...";

        public override string ShortDisplayName => string.Empty;

        public override string DestinationPath { get; set; }
    }
}