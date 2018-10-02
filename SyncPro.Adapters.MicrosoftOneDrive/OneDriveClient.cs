namespace SyncPro.Adapters.MicrosoftOneDrive
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading.Tasks;
    using System.Web;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    using SyncPro.Adapters.MicrosoftOneDrive.DataModel;
    using SyncPro.Counters;
    using SyncPro.OAuth;
    using SyncPro.Tracing;

    public class AuthenticationResult
    {
        public string Code { get; set; }
    }

    internal class OneDriveResponse<T>
    {
        [JsonProperty("@odata.context")]
        public string Context { get; set; }

        [JsonProperty("@odata.nextLink")]
        public string NextLink { get; set; }

        [JsonProperty("@odata.deltaLink")]
        public string DeltaLink { get; set; }

        [JsonProperty("@delta.token")]
        public string DeltaToken{ get; set; }

        [JsonProperty("value")]
        public T Value { get; set; }

        public OneDriveResponse()
        {
        }

        public OneDriveResponse(T value)
        {
            this.Value = value;
        }
    }

    public class OneDriveErrorResponseContainer
    {
        [JsonProperty("error")]
        public OneDriveErrorResponse ErrorResponse { get; set; }
    }

    public class OneDriveErrorResponse
    {
        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("innererror")]
        public OneDriveErrorResponse InnerError { get; set; }
    }

    public class OneDriveClient : IDisposable
    {
        public const string OneDriveTokenEndpoint = "https://login.live.com/oauth20_token.srf";
        public const string OneDriveApiBaseAddress = "https://api.onedrive.com";
        public const string LiveApiBaseAddress = "https://apis.live.net";
        public const string SyncProAppId = "00000000401FEC14";
        public const string DefaultReturnUri = "urn:ietf:wg:oauth:2.0:oob";

        private HttpClient oneDriveHttpClient;
        private HttpClient oneDriveHttpClientNoRedirect;
        private HttpClient liveHttpClient;

        public TokenResponse CurrentToken { get; private set; }

        public event EventHandler<TokenRefreshedEventArgs> TokenRefreshed;

        private readonly List<OneDriveUploadSession> uploadSessions = new List<OneDriveUploadSession>();

        public static async Task<TokenResponse> GetAccessToken(AuthenticationResult authenticationResult)
        {
            using (HttpClient client = new HttpClient())
            {
                Dictionary<string, string> paramList = new Dictionary<string, string>
                {
                    ["client_id"] = SyncProAppId,
                    ["redirect_uri"] = DefaultReturnUri,
                    ["code"] = authenticationResult.Code,
                    ["grant_type"] = "authorization_code"
                };

                FormUrlEncodedContent content = new FormUrlEncodedContent(paramList);

                var postResult = await client.PostAsync(OneDriveTokenEndpoint, content).ConfigureAwait(false);

                string postContent = await postResult.Content.ReadAsStringAsync().ConfigureAwait(false);

                var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(postContent);

                tokenResponse.AcquireTime = DateTime.Now;

                return tokenResponse;
            }
        }

        public OneDriveClient(TokenResponse token)
        {
            Pre.ThrowIfArgumentNull(token, nameof(token));

            this.CurrentToken = token;


            this.oneDriveHttpClient = new HttpClient()
            {
                BaseAddress = new Uri(OneDriveApiBaseAddress),
            };

            HttpClientHandler noRedirectHandler = new HttpClientHandler { AllowAutoRedirect = false };

            this.oneDriveHttpClientNoRedirect = new HttpClient(noRedirectHandler)
            {
                BaseAddress = new Uri(OneDriveApiBaseAddress),
            };

            this.liveHttpClient = new HttpClient()
            {
                BaseAddress = new Uri(LiveApiBaseAddress),
            };
        }

        public async Task<Drive> GetDefaultDrive()
        {
            OneDriveResponse<Drive> response = await this.GetOneDriveItem<Drive>("/v1.0/drive").ConfigureAwait(false);
            return response.Value;
        }

        public async Task<UserProfile> GetUserProfileAsync()
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, LiveApiBaseAddress + "/v5.0/me");
            HttpResponseMessage response = await this.SendLiveRequest(request).ConfigureAwait(false);

            // Request was successful. Read the content returned.
            string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            return JsonConvert.DeserializeObject<UserProfile>(content);
        }

        public async Task<Item> GetItemByItemIdAsync(string itemId)
        {
            string requestUri = string.Format("/v1.0/drive/items/{0}", itemId);
            var response = await this.GetOneDriveItem<Item>(requestUri).ConfigureAwait(false);
            return response.Value;
        }

        public async Task<Item> GetItemByPathAsync(string path)
        {
            string requestUri = "/v1.0/drive/root:/" + path.TrimStart('/');
            var response = await this.GetOneDriveItem<Item>(requestUri).ConfigureAwait(false);
            return response.Value;
        }

        public async Task<Item> GetOrCreateFolderAsync(ItemContainer parent, string name)
        {
            var children = await this.GetChildItems(parent).ConfigureAwait(false);

            var existingChild = children.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existingChild != null)
            {
                return existingChild;
            }

            return await this.CreateFolderAsync(parent, name).ConfigureAwait(false);
        }

        public async Task<Item> CreateFolderAsync(ItemContainer parent, string name)
        {
            string requestUri;

            // Build the request specific to the parent
            if (parent.IsItem)
            {
                requestUri = string.Format("/v1.0/drive/items/{0}/children", parent.Item.Id);
            }
            else
            {
                requestUri = string.Format("/v1.0/drives/{0}/root/children", parent.Drive.Id);
            }

            Item newFolder = new Item
            {
                Name = name,
                Folder = new FolderFacet()
            };

            string jsonContent = JsonConvert.SerializeObject(newFolder, new JsonSerializerSettings()
            {
                DefaultValueHandling = DefaultValueHandling.Ignore
            });

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUri);
            request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            CounterManager.LogSyncJobCounter(
                Constants.CounterNames.ApiCall,
                1,
                new CounterDimension(
                    Constants.DimensionNames.OperationName,
                    "CreateFolder"));

            HttpResponseMessage response = await this.SendOneDriveRequest(request).ConfigureAwait(false);

            return await response.Content.ReadAsJsonAsync<Item>().ConfigureAwait(false);
        }

        public async Task<ThumbnailSet> GetThumbnailsAsync(string itemId)
        {
            string requestUri = "/v1.0/drive/items/" + itemId + "/thumbnails";
            var response = await this.GetOneDriveItem<ThumbnailSet>(requestUri).ConfigureAwait(false);
            return response.Value;
        }

        public async Task<byte[]> GetRawBytesFromOneDrive(string url)
        {
            return await this.oneDriveHttpClient.GetByteArrayAsync(url).ConfigureAwait(false);
        }

        private async Task<OneDriveResponse<T>> GetOneDriveItem<T>(string requestUri)
        {
            CounterManager.LogSyncJobCounter(
                Constants.CounterNames.ApiCall,
                1,
                new CounterDimension(
                    Constants.DimensionNames.OperationName,
                    "GetItem"));

            // Send the request to OneDrive and get the response.
            var response = await this.SendOneDriveRequest(new HttpRequestMessage(HttpMethod.Get, requestUri)).ConfigureAwait(false);

            // Request was successful. Read the content returned.
            string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            T resultObject = JsonConvert.DeserializeObject<T>(content);
            return new OneDriveResponse<T>(resultObject);
        }

        private async Task<OneDriveResponse<T>> GetOneDriveItemSet<T>(string requestUri)
        {
            CounterManager.LogSyncJobCounter(
                Constants.CounterNames.ApiCall,
                1,
                new CounterDimension(
                    Constants.DimensionNames.OperationName,
                    "GetItemSet"));

            // Send the request to OneDrive and get the response.
            var response = await this.SendOneDriveRequest(new HttpRequestMessage(HttpMethod.Get, requestUri)).ConfigureAwait(false);

            // Request was successful. Read the content returned.
            string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            return JsonConvert.DeserializeObject<OneDriveResponse<T>>(content);
        }

        private async Task<HttpResponseMessage> SendOneDriveRequest(HttpRequestMessage request)
        {
            var response = await this.SendOneDriveRequest(request, this.oneDriveHttpClient).ConfigureAwait(false);

            // Any failures (including those from re-issuing after a refresh) will ne handled here
            if (!response.IsSuccessStatusCode)
            {
                throw OneDriveHttpException.FromResponse(response);
            }

            return response;
        }

        /// <summary>
        /// Send an HTTP request to the OneDrive endpoint, handling the case when a token refresh is required.
        /// </summary>
        /// <remarks>
        /// The caller must provide the request to send. The authentication header will be set by this method. Any error
        /// returned by the call (including failure to refresh the token) will result in an exception being thrown.
        /// </remarks>
        private async Task<HttpResponseMessage> SendOneDriveRequest(HttpRequestMessage request, HttpClient client)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("bearer", this.CurrentToken.AccessToken);
            LogRequest(request, client.BaseAddress);
            var response = await client.SendAsync(request).ConfigureAwait(false);
            LogResponse(response);

            // Check for token refresh
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                IEnumerable<string> values;
                if (response.Headers.TryGetValues("Www-Authenticate", out values))
                {
                    Dictionary<string, string> authErrorElements = GetAuthenticationHeaderError(response);
                    if (authErrorElements.GetValueOrDefault("error") == "expired_token")
                    {
                        // The access token is expired. Refresh the token, then re-issue the request.
                        await this.RefreshToken().ConfigureAwait(false);

                        var newRequest = await request.Clone().ConfigureAwait(false);

                        // Re-add the access token now that it has been refreshed.
                        newRequest.Headers.Authorization = new AuthenticationHeaderValue("bearer", this.CurrentToken.AccessToken);
                        LogRequest(newRequest, client.BaseAddress);

                        // Dispose of the previous response before creating the new one
                        response.Dispose();

                        response = await client.SendAsync(newRequest).ConfigureAwait(false);
                        LogResponse(response);
                    }
                }
            }

            return response;
        }

        private static void LogRequest(HttpRequestMessage request, Uri defaultBaseAddress)
        {
            LogRequest(request, defaultBaseAddress, false);
        }

        private static void LogRequest(HttpRequestMessage request, Uri defaultBaseAddress, bool includeDetail)
        {
            Uri uri = request.RequestUri;

            if (!uri.IsAbsoluteUri)
            {
                uri = new Uri(defaultBaseAddress, uri);
            }

            uri = uri.ReplaceQueryParameterIfExists("access_token", "<removed>");

            Logger.Debug("HttpRequest: {0} to {1}", request.Method, uri);

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

        /// <summary>
        /// Send an HTTP request to the Live endpoint, handling the case when a token refresh is required.
        /// </summary>
        /// <remarks>
        /// The caller must provide the request to send. The authentication header will be set by this method. Any error
        /// returned by the call (including failure to refresh the token) will result in an exception being thrown.
        /// </remarks>
        private async Task<HttpResponseMessage> SendLiveRequest(HttpRequestMessage request)
        {
            var qsParams = request.RequestUri.GetQueryParameters();
            qsParams["access_token"] = this.CurrentToken.AccessToken;

            var uriParts = request.RequestUri.ToString().Split(new [] {'?'}, 2);
            request.RequestUri = new Uri(uriParts[0] + UriExtensions.CombineQueryString(qsParams));

            LogRequest(request, this.liveHttpClient.BaseAddress);
            HttpResponseMessage response = await this.liveHttpClient.SendAsync(request).ConfigureAwait(false);
            LogResponse(response);

            // Check for token refresh
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                string responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                bool refreshToken = false;

                try
                {
                    JObject jObject = JObject.Parse(responseContent);
                    refreshToken = Convert.ToString(jObject["error"]["code"]) == "request_token_expired";
                }
                catch
                {
                    // There was an error parsing the response from the server.
                }

                if (refreshToken)
                {
                    // Refresh the current token
                    await this.RefreshToken().ConfigureAwait(false);

                    var newRequest = await request.Clone().ConfigureAwait(false);

                    // Resend the request using the new token
                    qsParams = newRequest.RequestUri.GetQueryParameters();
                    qsParams["access_token"] = this.CurrentToken.AccessToken;

                    uriParts = newRequest.RequestUri.ToString().Split(new[] { '?' }, 2);
                    newRequest.RequestUri = new Uri(uriParts[0] + UriExtensions.CombineQueryString(qsParams));
                    LogRequest(request, this.liveHttpClient.BaseAddress);
                    response = await this.liveHttpClient.SendAsync(newRequest).ConfigureAwait(false);
                    LogResponse(response);
                }
            }

            // Any failures (including those from re-issuing after a refresh) will be handled here
            if (!response.IsSuccessStatusCode)
            {
                var exception = new OneDriveHttpException("Live exception", response.StatusCode);
                exception.Data["Content"] = response.Content.ReadAsStringAsync().Result;
                throw exception;
            }

            return response;
        }

        private async Task RefreshToken()
        {
            Logger.Info("Refreshing Live token");

            using (HttpClient client = new HttpClient())
            {
                Dictionary<string, string> paramList = new Dictionary<string, string>
                {
                    ["client_id"] = SyncProAppId,
                    ["redirect_uri"] = DefaultReturnUri,
                    ["refresh_token"] = this.CurrentToken.RefreshToken,
                    ["grant_type"] = "refresh_token"
                };

                FormUrlEncodedContent content = new FormUrlEncodedContent(paramList);

                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, OneDriveTokenEndpoint)
                {
                    Content = content
                };

                LogRequest(request, client.BaseAddress);
                HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
                LogResponse(response);

                if (!response.IsSuccessStatusCode)
                {
                    // This will throw an exception according to the type of failure that occurred
                    await HandleTokenRefreshFailure(response).ConfigureAwait(false);
                }

                string responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                this.CurrentToken = JsonConvert.DeserializeObject<TokenResponse>(responseContent);

                // We just acquired a new token through a refresh, so update the acquire time accordingly.
                this.CurrentToken.AcquireTime = DateTime.Now;

                this.TokenRefreshed?.Invoke(this, new TokenRefreshedEventArgs { NewToken = this.CurrentToken });
            }
        }

        private static Dictionary<string, string> GetAuthenticationHeaderError(HttpResponseMessage response)
        {
            IEnumerable<string> values;
            if (response.Headers.TryGetValues("Www-Authenticate", out values))
            {
                // Should return a value simiar to the following:
                // Bearer realm="OneDriveAPI", error="expired_token", error_description="Auth token expired. Try refreshing."
                // First is to split the 'Bearer' portion 
                string[] headerValue = values.First().Split(new[] { ' ' }, 2);
                Pre.Assert(headerValue[0] == "Bearer", "headerValue[0] == Bearer");
                if (headerValue.Length == 2)
                {
                    // Next split into response components. There should be a max of 3.
                    string[] responseParts = headerValue[1].Split(new[] { ',' }, 3);
                    return responseParts
                        .Select(responsePart => responsePart.Trim())
                        .Select(str => str.Split(new[] {'='}, 2))
                        .Where(elements => elements.Length == 2)
                        .ToDictionary(elements => elements[0], elements => elements[1].Trim('\"'));
                }
            }

            return new Dictionary<string, string>();
        }

        private static async Task HandleTokenRefreshFailure(HttpResponseMessage response)
        {
            // OneDrive specific logic: If the refresh token is expired, the server will return a 400 Bad Request
            // response with json content saying that the user must sign in.
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                WindowsLiveError errorData = await response.Content.TryReadAsJsonAsync<WindowsLiveError>().ConfigureAwait(false);
                if (errorData != null && errorData.Error == "invalid_grant")
                {
                    throw new OneDriveTokenRefreshFailedException("The refresh token is expired.", errorData);
                }
            }

            // Dev note: Try to understand all of the refresh token failures. Any expected failures should be
            // throw as OneDriveTokenRefreshFailedException. This is here as a catch-all.
            var exception = new OneDriveHttpException("Failed to refresh token.", response.StatusCode);

            if (response.Headers.Contains("WwwAuthenticate"))
            {
                exception.Data["HttpAuthenticationHeader"] = response.Headers.WwwAuthenticate;
            }

            throw exception;
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
                foreach (OneDriveUploadSession session in this.uploadSessions)
                {
                    Logger.Info("Cancelling incomplete upload session for " + session.ItemName);

                    try
                    {
                        this.CancelUploadSession(session).Wait();
                    }
                    catch (Exception exception)
                    {
                        Logger.Info("Suppressing exception from CancelUploadSession(): " + exception.Message);
                    }
                }

                this.oneDriveHttpClient.Dispose();
                this.oneDriveHttpClient = null;

                this.oneDriveHttpClientNoRedirect.Dispose();
                this.oneDriveHttpClientNoRedirect = null;

                this.liveHttpClient.Dispose();
                this.liveHttpClient = null;
            }

            // free native resources if there are any.
        }

        public async Task CancelUploadSession(OneDriveUploadSession session)
        {
            // If the session is already cancelled, nothing to do
            if (session.State == OneDriveFileUploadState.Cancelled)
            {
                return;
            }

            CounterManager.LogSyncJobCounter(
                Constants.CounterNames.ApiCall,
                1,
                new CounterDimension(
                    Constants.DimensionNames.OperationName,
                    "CancelUploadSession"));

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Delete, session.UploadUrl);
            HttpResponseMessage response = await this.SendOneDriveRequest(request).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                session.State = OneDriveFileUploadState.Cancelled;
                return;
            }

            Logger.Warning(
                "Failed to cancel upload session for file {0} (ParenId={1})",
                session.ItemName,
                session.ParentId);

            LogRequest(request, this.oneDriveHttpClient.BaseAddress, true);
            LogResponse(response, true);

            throw new OneDriveHttpException("Failed to cancel the upload session.", response.StatusCode);
        }

        public async Task<IEnumerable<Item>> GetChildItems(ItemContainer parent)
        {
            string requestUri;

            // Build the request specific to the parent
            if (parent.IsItem)
            {
                // If we know the item is NOT a folder, or if it is a folder and has no children, return an empty list since
                // we know that it will not have any child items.
                if (parent.Item.Folder == null || parent.Item.Folder.ChildCount == 0)
                {
                    return new List<Item>();
                }

                requestUri = string.Format("/v1.0/drive/items/{0}/children", parent.Item.Id);
            }
            else 
            {
                requestUri = string.Format("/v1.0/drives/{0}/root/children", parent.Drive.Id);
            }

            List<Item> items = new List<Item>();
            while (true)
            {
                OneDriveResponse<Item[]> oneDriveResponse =
                    await this.GetOneDriveItemSet<Item[]>(requestUri).ConfigureAwait(false);

                items.AddRange(oneDriveResponse.Value);

                if (string.IsNullOrWhiteSpace(oneDriveResponse.NextLink))
                {
                    break;
                }

                requestUri = oneDriveResponse.NextLink;
            }

            return items;
        }

        public async Task<Item> CreateItem(ItemContainer parent, string name)
        {
            string uri = string.Format("/v1.0/drive/items/{0}:/{1}:/content?@name.conflictBehavior=fail", parent.Item.Id, HttpUtility.UrlEncode(name));
            HttpContent requestContent = new StringContent(string.Empty, Encoding.ASCII, "text/plain");
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, uri)
            {
                Content = requestContent
            };

            CounterManager.LogSyncJobCounter(
                Constants.CounterNames.ApiCall,
                1,
                new CounterDimension(
                    Constants.DimensionNames.OperationName,
                    "CreateItem"));

            var response = await this.SendOneDriveRequest(request).ConfigureAwait(false);

            // Request was successful. Read the content returned.
            string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            return JsonConvert.DeserializeObject<Item>(content);
        }

        // See https://docs.microsoft.com/en-us/onedrive/developer/rest-api/api/driveitem_createuploadsession
        public async Task<OneDriveUploadSession> CreateUploadSession(string parentItemId, string name, long length)
        {
            if (string.IsNullOrWhiteSpace(parentItemId))
            {
                throw new ArgumentNullException(nameof(parentItemId));
            }

            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            // TODO: Check for the maximum file size limit
            if (length <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            if (this.uploadSessions.Any(s => s.ParentId == parentItemId && s.ItemName == name))
            {
                throw new InvalidOperationException("An upload session for this item already exists.");
            }

            HttpRequestMessage request = new HttpRequestMessage(
                HttpMethod.Post, 
                string.Format("/v1.0/drive/items/{0}:/{1}:/upload.createSession", parentItemId, HttpUtility.UrlEncode(name)));

            CounterManager.LogSyncJobCounter(
                Constants.CounterNames.ApiCall,
                1,
                new CounterDimension(
                    Constants.DimensionNames.OperationName,
                    "CreateUploadSession"));

            HttpResponseMessage response = await this.SendOneDriveRequest(request).ConfigureAwait(false);

            JObject responseObject = await response.Content.ReadAsJObjectAsync().ConfigureAwait(false);

            var newSession = new OneDriveUploadSession(
                parentItemId,
                name,
                responseObject["uploadUrl"].Value<string>(),
                responseObject["expirationDateTime"].Value<DateTime>(),
                length);

            Logger.Info(
                "Created OneDrive upload session with parentItemId={0}, name={1}, expirationDateTime={2}",
                parentItemId,
                name,
                newSession.ExpirationDateTime);

            this.uploadSessions.Add(newSession);

            return newSession;
        }

        public async Task SendUploadFragment(OneDriveUploadSession uploadSession, byte[] fragmentBuffer, long offset)
        {
            switch (uploadSession.State)
            {
                case OneDriveFileUploadState.NotStarted:
                    uploadSession.State = OneDriveFileUploadState.InProgress;
                    break;
                case OneDriveFileUploadState.Completed:
                    throw new OneDriveException("Cannot upload fragment to completed upload session.");
                case OneDriveFileUploadState.Faulted:
                    throw new OneDriveException("Cannot upload fragment to faulted upload session.");
                case OneDriveFileUploadState.Cancelled:
                    throw new OneDriveException("Cannot upload fragment to cancelled upload session.");
            }

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, uploadSession.UploadUrl)
            {
                Content = new ByteArrayContent(fragmentBuffer)
            };

            request.Content.Headers.ContentLength = fragmentBuffer.LongLength;
            request.Content.Headers.ContentRange = new ContentRangeHeaderValue(
                offset, 
                offset + fragmentBuffer.Length - 1,
                uploadSession.Length);

            CounterManager.LogSyncJobCounter(
                Constants.CounterNames.ApiCall,
                1,
                new CounterDimension(
                    Constants.DimensionNames.OperationName,
                    "SendUploadFragment"));

            var response = await this.SendOneDriveRequest(request).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                uploadSession.State = OneDriveFileUploadState.Faulted;
            }

            if (response.StatusCode == HttpStatusCode.Created || response.StatusCode == HttpStatusCode.OK)
            {
                // 201 indicates that the upload is complete.
                uploadSession.State = OneDriveFileUploadState.Completed;
                uploadSession.Item = await response.Content.ReadAsJsonAsync<Item>().ConfigureAwait(false);

                this.uploadSessions.Remove(uploadSession);
            }
        }

        public async Task<HttpResponseMessage> DownloadFileFragment(Uri downloadUri, int offset, int length)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, downloadUri);
            request.Headers.Range = new RangeHeaderValue(offset * length, ((offset + 1) * length) - 1);

            CounterManager.LogSyncJobCounter(
                Constants.CounterNames.ApiCall,
                1,
                new CounterDimension(
                    Constants.DimensionNames.OperationName,
                    "DownloadFileFragment"));

            var response = await this.oneDriveHttpClient.SendAsync(request).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new OneDriveHttpException(response.ReasonPhrase, response.StatusCode);
            }

            return response;
        }

        public async Task<Uri> GetDownloadUriForItem(string id)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "/v1.0/drive/items/" + id + "/content");

            CounterManager.LogSyncJobCounter(
                Constants.CounterNames.ApiCall,
                1,
                new CounterDimension(
                    Constants.DimensionNames.OperationName,
                    "GetDownloadUriForItem"));

            var response = await this.SendOneDriveRequest(request, this.oneDriveHttpClientNoRedirect).ConfigureAwait(false);

            return response.Headers.Location;
        }

        public async Task<OneDriveDeltaView> GetDeltaView(string path, string previousDeltaToken)
        {
            if (!path.StartsWith("/"))
            {
                throw new ArgumentException("The path is expected to start with a / character", nameof(path));
            }

            string requestUri = string.Format("{0}/v1.0/drive/root:{1}:/view.delta", OneDriveApiBaseAddress, path);

            if (string.IsNullOrWhiteSpace(previousDeltaToken))
            {
                Logger.Debug("OneDriveClient: Requesting delta view with null delta token");
            }
            else
            {
                Logger.Debug("OneDriveClient: Requesting delta view with non-null delta token");
                requestUri += "?token=" + previousDeltaToken;
            }

            return await this.GetDeltaView(requestUri).ConfigureAwait(false);
        }

        public async Task<OneDriveDeltaView> GetDeltaView(string requestUri)
        {
            // A delta view from OneDrive can be larger than a single request, so loop until we have built the complete
            // view by following the NextLink properties.
            OneDriveDeltaView deltaView = new OneDriveDeltaView();
            while (true)
            {
                OneDriveResponse<Item[]> oneDriveResponse =
                    await this.GetOneDriveItemSet<Item[]>(requestUri).ConfigureAwait(false);

                deltaView.Items.AddRange(oneDriveResponse.Value);

                if (string.IsNullOrWhiteSpace(oneDriveResponse.NextLink))
                {
                    deltaView.Token = oneDriveResponse.DeltaToken;
                    deltaView.DeltaLink = oneDriveResponse.DeltaLink;

                    break;
                }

                requestUri = oneDriveResponse.NextLink;
            }

            return deltaView;
        }

        public static string GetDefaultLogoutUri()
        {
            // Build the URI to log out the current user. We need to do this in order to ensure that the user is correctly
            // for their credentials. Otherwise, the login page will attempt to use the current user's information (via a \
            // cookie), and thus we wont be able to get a token for anyone other than the current user.
            return string.Format(
                "https://login.live.com/oauth20_logout.srf?client_id={0}&redirect_uri={1}",
                OneDriveClient.SyncProAppId,
                OneDriveClient.DefaultReturnUri);
        }

        public static string GetDefaultAuthorizationUri()
        {
            // Build the URI that will show the live authorization page.
            return string.Format(
                "https://login.live.com/oauth20_authorize.srf?client_id={0}&scope={1}&response_type=code&redirect_uri={2}",
                OneDriveClient.SyncProAppId,
                HttpUtility.UrlEncode(string.Join(" ", "onedrive.readwrite", "wl.signin", "wl.offline_access", "wl.basic")),
                OneDriveClient.DefaultReturnUri);
        }
    }

    public class OneDriveDeltaView
    {
        public List<Item> Items { get; }

        public string Token { get; set; }

        public string DeltaLink { get; set; }

        public OneDriveDeltaView()
        {
            this.Items = new List<Item>();
        }
    }
}