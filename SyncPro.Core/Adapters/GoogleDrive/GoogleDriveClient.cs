namespace SyncPro.Adapters.GoogleDrive
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading.Tasks;
    using System.Web;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    using SyncPro.Adapters.GoogleDrive.DataModel;
    using SyncPro.Adapters.MicrosoftOneDrive;
    using SyncPro.OAuth;
    using SyncPro.Tracing;

    public class AuthenticationResult
    {
        public string Code { get; set; }
    }

    public class GoogleDriveClient: IDisposable
    {
        public const string GoogleDriveApiBaseAddress = "https://www.googleapis.com/drive/v3";
        public const string GoogleDriveTokenEndpoint = "https://www.googleapis.com/oauth2/v4/token";
        public const string SyncProAppId = "667622065430-ntj3cgv04k9t0n4jt645iiec6tr846gd.apps.googleusercontent.com";
        public const string SyncProAppSecret = "QedQTxhDzaMKEt7ygWPiHXl7";
        public const string DefaultReturnUri = "http://127.0.0.1/";

        private HttpClient googleDriveHttpClient;

        public TokenResponse CurrentToken { get; private set; }

        public event EventHandler<TokenRefreshedEventArgs> TokenRefreshed;


        public GoogleDriveClient(TokenResponse token)
        {
            Pre.ThrowIfArgumentNull(token, nameof(token));

            this.CurrentToken = token;

            HttpClientHandler handler = new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            this.googleDriveHttpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri(GoogleDriveApiBaseAddress)
            };

            this.googleDriveHttpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "SyncPro/1.0 (gzip)");
        }

        public async Task<User> GetUserInformation()
        {
            string fields = HttpUtility.UrlEncode("email,family_name,gender,given_name,hd,id,link,locale,name,picture,verified_email");
            string requestUri = "https://www.googleapis.com/oauth2/v2/userinfo?fields=" + fields;

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            HttpResponseMessage response = await this.SendRequest(request).ConfigureAwait(false);

            // Request was successful. Read the content returned.
            string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            return JsonConvert.DeserializeObject<User>(content);
        }

        public async Task<User> GetDriveInformation()
        {
            string fields = HttpUtility.UrlEncode("appInstalled,kind,maxImportSizes,maxUploadSize,storageQuota,user");
            string requestUri = GoogleDriveApiBaseAddress + "/about?fields=" + fields;

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            HttpResponseMessage response = await this.SendRequest(request).ConfigureAwait(false);

            // Request was successful. Read the content returned.
            string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            return JsonConvert.DeserializeObject<User>(content);
        }

        private async Task<HttpResponseMessage> SendRequest(HttpRequestMessage request)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", this.CurrentToken.AccessToken);
            request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));

            LogRequest(request);
            var response = await this.googleDriveHttpClient.SendAsync(request).ConfigureAwait(false);
            LogResponse(response);


            if (!response.IsSuccessStatusCode)
            {
                Debugger.Break();
            }

            // Check for token refresh
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                string responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                bool performTokenRefresh = false;
                try
                {
                    JObject jObject = JObject.Parse(responseContent);
                    var errorObject = jObject["error"];
                    var firstError = errorObject["errors"][0];
                    performTokenRefresh = firstError["reason"].Value<string>() == "authError";
                }
                catch (Exception exception)
                {
                    Logger.Warning("Failed to determine failure. " + exception);
                }

                if (performTokenRefresh)
                {
                    // The access token is expired. Refresh the token, then re-issue the request.
                    await this.RefreshToken().ConfigureAwait(false);

                    // Re-add the access token now that it has been refreshed.
                    var newRequest = await request.Clone().ConfigureAwait(false);
                    newRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", this.CurrentToken.AccessToken);

                    LogRequest(request);
                    response = await this.googleDriveHttpClient.SendAsync(newRequest).ConfigureAwait(false);
                    LogResponse(response);
                }
            }

            // Any failures (including those from re-issuing after a refresh) will be handled here
            if (!response.IsSuccessStatusCode)
            {
                var exception = new Exception("Live exception");
                exception.Data["HttpStatusCode"] = response.StatusCode;
                exception.Data["Content"] = response.Content.ReadAsStringAsync().Result;
                throw exception;
            }

            return response;
        }

        private async Task RefreshToken()
        {
            Logger.Info("Refreshing OAuth token for Google Drive");

            using (HttpClient client = new HttpClient())
            {
                Dictionary<string, string> paramList = new Dictionary<string, string>
                {
                    ["client_id"] = SyncProAppId,
                    ["client_secret"] = SyncProAppSecret,
                    ["refresh_token"] = this.CurrentToken.RefreshToken,
                    ["grant_type"] = "refresh_token"
                };

                FormUrlEncodedContent content = new FormUrlEncodedContent(paramList);

                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, GoogleDriveTokenEndpoint)
                {
                    Content = content
                };

                LogRequest(request);
                HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
                LogResponse(response);

                if (!response.IsSuccessStatusCode)
                {
                    // TODO: Replace with the correct exception type (non-onedrive)
                    var exception = new OneDriveHttpException("Failed to refresh token.", response.StatusCode);
                    exception.Data["HttpAuthenticationHeader"] = response.Headers.WwwAuthenticate;

                    throw exception;
                }

                // Read the content from the refresh request and convert to a TokenResponse object.
                string responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var newToken = JsonConvert.DeserializeObject<TokenResponse>(responseContent);

                // Google APIs dont return the refresh token, scopes, etc, so we need to set all of that here. Also note that
                // unline OneDrive, the refresh token does not change with a refresh.
                newToken.AcquireTime = DateTime.Now;
                newToken.RefreshToken = this.CurrentToken.RefreshToken;

                this.CurrentToken = newToken;

                this.TokenRefreshed?.Invoke(this, new TokenRefreshedEventArgs { NewToken = this.CurrentToken });
            }
        }

        private static void LogRequest(HttpRequestMessage request)
        {
            LogRequest(request, false);
        }

        private static void LogRequest(HttpRequestMessage request, bool includeDetail)
        {
            Logger.Debug("HttpRequest: {0} to {1}", request.Method, request.RequestUri);

            if (!includeDetail)
            {
                return;
            }

            Logger.Debug("Headers:");

            foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers)
            {
                Logger.Debug("   {0} = {1}", header.Key, header.Value);
            }

            Logger.Debug("Properties:");

            foreach (KeyValuePair<string, object> property in request.Properties)
            {
                Logger.Debug("   {0} = {1}", property.Key, property.Value);
            }

            Logger.Debug("Content Headers:");

            foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers)
            {
                Logger.Debug("   {0} = {1}", header.Key, header.Value);
            }
        }

        private static void LogResponse(HttpResponseMessage response)
        {
            LogResponse(response, false);
        }

        private static void LogResponse(HttpResponseMessage response, bool includeDetail)
        {
            Logger.Debug("HttpResponse: {0} ({1})", (int)response.StatusCode, response.ReasonPhrase);

            if (!includeDetail)
            {
                return;
            }

            Logger.Debug("Headers:");

            foreach (KeyValuePair<string, IEnumerable<string>> header in response.Headers)
            {
                Logger.Debug("   {0} = {1}", header.Key, header.Value);
            }

            Logger.Debug("Properties:");

            Logger.Debug("Content Headers:");

            foreach (KeyValuePair<string, IEnumerable<string>> header in response.Headers)
            {
                Logger.Debug("   {0} = {1}", header.Key, header.Value);
            }
        }
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // free managed resources

                this.googleDriveHttpClient.Dispose();
                this.googleDriveHttpClient = null;

            }

            // free native resources if there are any.
        }

        public async Task<Item> GetItemById(string id)
        {
            string[] fields = { "id", "name", "mimeType", "createdTime", "modifiedTime", "md5Checksum", "size" };
            string requestUri = string.Format(
                GoogleDriveApiBaseAddress + "/files/{0}?fields={1}",
                id,
                HttpUtility.UrlEncode(string.Join(",", fields)));

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            HttpResponseMessage response = await this.SendRequest(request).ConfigureAwait(false);

            // Request was successful. Read the content returned.
            string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            return JsonConvert.DeserializeObject<Item>(content);
        }

        public async Task<IEnumerable<Item>> GetChildItems(GoogleDriveAdapterItem adapterItem)
        {
            string[] fields = { "files(id", "name", "mimeType", "createdTime", "modifiedTime", "md5Checksum", "size)" };
            string requestUri = string.Format(
                GoogleDriveApiBaseAddress + "/files?q={0}&fields={1}",
                HttpUtility.UrlEncode(string.Format("'{0}' in parents", adapterItem.Item.Id)),
                HttpUtility.UrlEncode(string.Join(",", fields)));

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            HttpResponseMessage response = await this.SendRequest(request).ConfigureAwait(false);

            // Request was successful. Read the content returned.
            string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            var itemList = JsonConvert.DeserializeObject<ItemList>(content);
            return itemList.Files;
        }

        public async Task<HttpResponseMessage> DownloadFileFragment(string itemId, int offset, int length)
        {
            string requestUri = string.Format(GoogleDriveApiBaseAddress + "/files/{0}?alt=media", itemId);

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Range = new RangeHeaderValue(offset * length, ((offset + 1) * length) - 1);

            var response = await this.SendRequest(request).ConfigureAwait(false);
            //var response = await this.googleDriveHttpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                // TODO: Replace with correct exception
                throw new OneDriveHttpException(response.ReasonPhrase, response.StatusCode);
            }

            return response;
        }

        public static async Task<TokenResponse> GetAccessToken(AuthenticationResult authenticationResult, string codeVerifier)
        {
            using (HttpClient client = new HttpClient())
            {
                Dictionary<string, string> paramList = new Dictionary<string, string>();
                paramList["code"] = authenticationResult.Code;
                paramList["redirect_uri"] = GoogleDriveClient.DefaultReturnUri;
                paramList["code_verifier"] = codeVerifier;
                paramList["client_id"] = GoogleDriveClient.SyncProAppId;
                paramList["client_secret"] = GoogleDriveClient.SyncProAppSecret;
                paramList["grant_type"] = "authorization_code";

                FormUrlEncodedContent content = new FormUrlEncodedContent(paramList);


                var postResult = await client.PostAsync("https://www.googleapis.com/oauth2/v4/token", content).ConfigureAwait(false);

                string postContent = await postResult.Content.ReadAsStringAsync().ConfigureAwait(false);

                JObject jObject = JObject.Parse(postContent);

                JArray array = new JArray(new string[]
                {
                    "openid",
                    "https://www.googleapis.com/auth/drive",
                    "https://www.googleapis.com/auth/userinfo.email",
                    "https://www.googleapis.com/auth/userinfo.profile"
                });

                jObject["acquire_time"] = DateTime.Now;
                jObject["scopes"] = array;

                TokenResponse tokenResponse = new TokenResponse();

                tokenResponse.AccessToken = Convert.ToString(jObject["access_token"]);
                tokenResponse.RefreshToken = Convert.ToString(jObject["refresh_token"]);
                tokenResponse.IdToken = Convert.ToString(jObject["id_token"]);

                return tokenResponse;
                /*

                string fields = HttpUtility.UrlEncode("appInstalled,kind,maxImportSizes,maxUploadSize,storageQuota,user");

                //var realRequest = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v1/tokeninfo?id_token=" + idToken);
                var realRequest = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/drive/v3/about?fields=" + fields);
                realRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResponse.AccessToken);
                var response = client.SendAsync(realRequest).Result;
                */
            }
        }
    }
}