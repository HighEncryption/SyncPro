namespace SyncPro.UI.ViewModels.Adapters
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using System.Web;
    using System.Windows.Input;
    using System.Windows.Interop;

    using SyncPro.Adapters.GoogleDrive;
    using SyncPro.UI.Dialogs;
    using SyncPro.UI.FolderBrowser;
    using SyncPro.UI.Framework.MVVM;

    using AuthenticationResult = SyncPro.Adapters.GoogleDrive.AuthenticationResult;

    public class GoogleDriveAdapterViewModel : SyncAdapterViewModel
    {
        public ICommand SignInCommand { get; }

        public ICommand BrowsePathCommand { get; }

        public override string DisplayName => "Google Drive";

        public override string ShortDisplayName => "GDrive";

        public override string LogoImage => "/SyncPro.UI;component/Resources/ProviderLogos/google_drive.png";

        public GoogleDriveAdapter Adapter => (GoogleDriveAdapter) this.AdapterBase;

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

        public GoogleDriveAdapterViewModel(GoogleDriveAdapter adapter) : base(adapter)
        {
            this.SignInCommand = new DelegatedCommand(this.SignIn, o => this.CanSignIn);
            this.BrowsePathCommand = new DelegatedCommand(this.BrowsePath);

            this.CanSignIn = true;
            this.SignInMessage = "Not Signed In";

            this.UpdateSignInButton();
        }

        private string targetPath;

        public override string DestinationPath
        {
            get { return this.targetPath; }
            set
            {
                this.SetPropertyDelegated("DestinationPath", this.targetPath, value, () =>
                {
                    this.targetPath = value;
                });
            }
        }

        public void UpdateSignInButton()
        {
            this.SignInButtonText = this.IsSignedIn ? "Sign out" : "Sign in to Google Drive";
        }

        private FolderBrowserViewModel viewModel;

        private void BrowsePath(object obj)
        {
            FolderBrowserWindow window = new FolderBrowserWindow();
            if (this.viewModel == null)
            {
                this.viewModel = new FolderBrowserViewModel(this.AdapterBase) { Message = "Select the destination folder" };
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
                                this.DestinationPath.Split(new[] { this.AdapterBase.PathSeparator },
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

        private static string Base64UrlencodeNoPadding(byte[] buffer)
        {
            string base64 = Convert.ToBase64String(buffer);

            // Converts base64 to base64url.
            base64 = base64.Replace("+", "-");
            base64 = base64.Replace("/", "_");
            // Strips padding.
            base64 = base64.Replace("=", "");

            return base64;
        }

        private static string RandomDataBase64Url(uint length)
        {
            RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
            byte[] bytes = new byte[length];
            rng.GetBytes(bytes);
            return Base64UrlencodeNoPadding(bytes);
        }

        private static byte[] Sha256(string inputStirng)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(inputStirng);
            SHA256Managed sha256 = new SHA256Managed();
            return sha256.ComputeHash(bytes);
        }

        private void SignIn(object obj)
        {
            string state = RandomDataBase64Url(32);
            string codeVerifier = RandomDataBase64Url(32);
            string codeChallenge = Base64UrlencodeNoPadding(Sha256(codeVerifier));
            const string CodeChallengeMethod = "S256";

            // Creates a redirect URI using an available port on the loopback address.
            string redirectUri = string.Format("http://{0}/", IPAddress.Loopback);

            string[] scopes = {
                "openid",
                "https://www.googleapis.com/auth/drive",
                "https://www.googleapis.com/auth/userinfo.email",
                "https://www.googleapis.com/auth/userinfo.profile"
            };

            string authUrl =
                string.Format(
                    "{0}?response_type=code&scope={6}&redirect_uri={1}&client_id={2}&state={3}&code_challenge={4}&code_challenge_method={5}",
                    "https://accounts.google.com/o/oauth2/v2/auth",
                    redirectUri,
                    GoogleDriveClient.SyncProAppId,
                    state,
                    codeChallenge,
                    CodeChallengeMethod,
                    HttpUtility.UrlEncode(string.Join(" ", scopes)));

            AuthenticationResult authenticationResult = null;
            BrowserAuthenticationWindow authWindow = new BrowserAuthenticationWindow();

            authWindow.Browser.Navigating += (sender, args) =>
            {
                if (args.Uri.ToString().StartsWith(redirectUri, StringComparison.OrdinalIgnoreCase))
                {
                    var qsList = args.Uri.Query.TrimStart('?')
                        .Split('&')
                        .ToDictionary(k => k.Split('=').First(), k => k.Split('=').Skip(1).First());

                    authenticationResult = new AuthenticationResult
                    {
                        Code = qsList["code"]
                    };

                    authWindow.Close();
                }
            };

            authWindow.Loaded += (sender, args) =>
            {
                authWindow.Browser.Navigate(authUrl);
                NativeMethods.User32.SetForegroundWindow(new WindowInteropHelper(authWindow).Handle);
            };

            authWindow.Closed += (sender, args) =>
            {
                if (authenticationResult == null)
                {
                    return;
                }

                this.CanSignIn = false;
                this.SignInMessage = "Working...";
                this.Adapter.SignIn(authenticationResult, codeVerifier).ContinueWith(this.SignInComplete);
            };

            //authWindow.Closing += (sender, e) =>
            //{
                
            //};


            authWindow.ShowDialog();


            /*
            // Build the URI that will show the login/authorization page.
            string authorizationUri =
                string.Format(
                    "https://login.live.com/oauth20_authorize.srf?client_id={0}&scope={1}&response_type=code&redirect_uri={2}",
                    GoogleDriveClient.SyncProAppId,
                    HttpUtility.UrlEncode(string.Join(" ", "onedrive.readwrite", "wl.signin", "wl.offline_access", "wl.basic")),
                    GoogleDriveClient.DefaultReturnUri);

            //AuthenticationResult authenticationResult = null;
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

                    // TODO: WHat here??
                    //authenticationResult = new AuthenticationResult()
                    //{
                    //    Code = queryParametes["code"]
                    //};

                    // All done. Close the window.
                    authWindow.Close();
                }
            };
            */

            //authWindow.Browser.LoadCompleted += (sender, args) =>
            //{
            //    if (string.Equals(args.Uri.AbsolutePath, "/oauth20_logout.srf", StringComparison.OrdinalIgnoreCase))
            //    {
            //        // The logout page has finished loading, so we can now load the login page.
            //        authWindow.Browser.Navigate(authorizationUri);
            //    }
            //};

            // TODO: Fix
            /*
            authWindow.Closed += (sender, args) =>
            {
                if (authenticationResult == null)
                {
                    return;
                }

                this.CanSignIn = false;
                this.SignInMessage = "Working...";
                this.AdapterBase.SignIn(authenticationResult).ContinueWith(this.SignInComplete);
            };

            authWindow.Loaded += (sender, args) =>
            {
                authWindow.Browser.Navigate(logoutUri);
                NativeMethods.SetForegroundWindow(new WindowInteropHelper(authWindow).Handle);
            };

            authWindow.ShowDialog();
            */

            //await this.SignInToMicrosoftAccount();

            //ODUserProfile profileInfo = await this.AdapterBase.GetUserProfileAsync();

            //if (profileInfo == null)
            //{
            //    this.SignInMessage = "Not Signed In";
            //    this.IsSignedIn = false;
            //}
            //else
            //{
            //    this.SignInMessage = string.Format("Signed in as {0} ({1})", profileInfo.Name, profileInfo.Emails.Account);
            //    this.IsSignedIn = true;
            //}
        }

        private void SignInComplete(Task obj)
        {
            var profile = this.Adapter.UserProfile;
            this.SignInMessage = !string.IsNullOrWhiteSpace(profile.Email)
                ? string.Format("Signed in as {0} ({1})", profile.Name, profile.Email)
                : string.Format("Signed in as {0}", profile.Name);

            this.IsSignedIn = true;
            this.CanSignIn = true;
            this.UpdateSignInButton();
            CommandManager.InvalidateRequerySuggested();
        }

        public static GoogleDriveAdapterViewModel CreateFromRelationship(SyncRelationshipViewModel relationship, bool isSourceAdapter)
        {
            ISyncTargetViewModel existingAdapter = isSourceAdapter ? relationship.SyncSourceAdapter : relationship.SyncDestinationAdapter;
            GoogleDriveAdapterViewModel model = existingAdapter as GoogleDriveAdapterViewModel;
            if (model != null)
            {
                return model;
            }

            return relationship.CreateAdapterViewModel<GoogleDriveAdapterViewModel>();
        }

        public override void LoadContext()
        {
            this.AdapterBase.LoadConfiguration();
        }

        public override void SaveContext()
        {
            this.AdapterBase.SaveConfiguration();
        }

        public override Type GetAdapterType()
        {
            return typeof(GoogleDriveAdapter);
        }
    }
}