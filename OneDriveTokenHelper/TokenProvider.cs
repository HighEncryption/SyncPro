namespace OneDriveTokenHelper
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Interop;

    using SyncPro;
    using SyncPro.Adapters.MicrosoftOneDrive;
    using SyncPro.OAuth;
    using SyncPro.UI.Dialogs;

    public static class TokenProvider
    {
        private static string tokenPath;
        private static readonly AutoResetEvent AuthComplete = new AutoResetEvent(false);

        public static bool TokenSuccess { get; set; }

        public static void SignIn(string path)
        {
            tokenPath = path;

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
                    Dictionary<string, string> queryParameters = args.Uri.GetQueryParameters();

                    authenticationResult = new AuthenticationResult()
                    {
                        Code = queryParameters["code"]
                    };

                    // All done. Close the window.
                    authWindow.Close();
                }
            };

            authWindow.Closed += (sender, args) =>
            {
                if (authenticationResult == null)
                {
                    AuthComplete.Set();
                    return;
                }

                Task.Factory.StartNew(async () =>
                {
                    TokenResponse currentToken = await OneDriveClient.GetAccessToken(authenticationResult).ConfigureAwait(false);
                    currentToken.SaveProtectedToken(tokenPath);
                    TokenSuccess = true;
                    AuthComplete.Set();
                });
            };

            authWindow.Loaded += (sender, args) =>
            {
                authWindow.Browser.Navigate(logoutUri);
                SyncPro.UI.NativeMethods.User32.SetForegroundWindow(new WindowInteropHelper(authWindow).Handle);
            };

            authWindow.ShowDialog();

            AuthComplete.WaitOne();
        }
    }
}