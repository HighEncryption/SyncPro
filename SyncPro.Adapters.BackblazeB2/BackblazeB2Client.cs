namespace SyncPro.Adapters.BackblazeB2
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Reflection;
    using System.Security;
    using System.Text;
    using System.Threading.Tasks;

    using Newtonsoft.Json.Linq;

    using SyncPro.Adapters.BackblazeB2.DataModel;
    using SyncPro.Counters;
    using SyncPro.Tracing;
    using SyncPro.Utility;

    using File = SyncPro.Adapters.BackblazeB2.DataModel.File;

    public class BackblazeB2Client : IDisposable
    {
        private readonly string accountId;

        private readonly SecureString applicationKey;

        private BackblazeConnectionInfo connectionInfo;

        private HttpClient httpClient;

        public event EventHandler<ConnectionInfoChangedEventArgs> ConnectionInfoChanged;

        private string userAgentString;

        public BackblazeB2Client(
            string accountId, 
            SecureString applicationKey, 
            BackblazeConnectionInfo connectionInfo)
        {
            this.accountId = accountId;
            this.applicationKey = applicationKey;
            this.connectionInfo = connectionInfo;

            this.userAgentString = string.Format(
                "SyncPro/{0}",
                Assembly.GetEntryAssembly().GetName().Version);
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
                this.httpClient.Dispose();
                this.httpClient = null;
            }

            // free native resources if there are any.
        }

        public async Task InitializeAsync()
        {
            this.httpClient = new HttpClient();

            if (this.connectionInfo == null)
            {
                await this.AuthorizeAccount().ConfigureAwait(false);
            }
        }

        public async Task<IList<Bucket>> ListBucketsAsync()
        {
            HttpRequestMessage request = this.BuildJsonRequest(
                Constants.ApiListBucketsUrl,
                HttpMethod.Post, 
                new JsonBuilder()
                    .AddProperty("accountId", this.accountId)
                    .ToString());

            List<Bucket> buckets;

            using (request)
            {
                HttpResponseMessage responseMessage =
                    await this.SendRequestAsync(request).ConfigureAwait(false);

                using (responseMessage)
                {
                    ListBucketsResponse response = 
                        await responseMessage.Content.TryReadAsJsonAsync<ListBucketsResponse>().ConfigureAwait(false);

                    buckets = response.Buckets.ToList();
                }
            }

            return buckets;
        }

        public async Task<Bucket> CreateBucket(string bucketName, string bucketType)
        {
            HttpRequestMessage request = this.BuildJsonRequest(
                Constants.ApiCreateBucketUrl,
                HttpMethod.Post,
                new JsonBuilder()
                    .AddProperty("accountId", this.accountId)
                    .AddProperty("bucketName", bucketName)
                    .AddProperty("bucketType", bucketType)
                    .ToString());

            Bucket response;

            using (request)
            {
                HttpResponseMessage responseMessage =
                    await this.SendRequestAsync(request).ConfigureAwait(false);

                using (responseMessage)
                {
                    response = await responseMessage.Content.TryReadAsJsonAsync<Bucket>().ConfigureAwait(false);
                }
            }

            return response;
        }

        private async Task<GetUploadUrlResponse> GetUploadUrl(string bucketId)
        {
            GetUploadUrlResponse response;

            HttpRequestMessage request = this.BuildJsonRequest(
                Constants.ApiGetUploadUrl,
                HttpMethod.Post,
                new JsonBuilder()
                    .AddProperty("bucketId", bucketId)
                    .ToString());

            using (request)
            {
                HttpResponseMessage responseMessage =
                    await this.SendRequestAsync(request).ConfigureAwait(false);

                using (responseMessage)
                {
                    response =
                        await responseMessage.Content.TryReadAsJsonAsync<GetUploadUrlResponse>().ConfigureAwait(false);
                }
            }

            return response;
        }

        public async Task<IList<File>> ListFileNamesAsync(
            string bucketId,
            string prefix, 
            string delimiter)
        {
            ListFileNamesResponse response = await InternalListFileNamesAsync(
                    bucketId,
                    prefix,
                    delimiter,
                    null)
                .ConfigureAwait(false);

            List<File> files = new List<File>(response.Files);

            while (!string.IsNullOrWhiteSpace(response.NextFileName))
            {
                response = await InternalListFileNamesAsync(
                        bucketId,
                        prefix,
                        delimiter,
                        response.NextFileName)
                    .ConfigureAwait(false);

                files.AddRange(response.Files);
            }

            return files;
        }

        private async Task<ListFileNamesResponse> InternalListFileNamesAsync(
            string bucketId,
            string prefix,
            string delimiter,
            string startFileName)
        {
            HttpRequestMessage request = this.BuildJsonRequest(
                Constants.ApiListFileNamesUrl,
                HttpMethod.Post, 
                new JsonBuilder()
                    .AddProperty("bucketId", bucketId)
                    .AddProperty("maxFileCount", 1000)
                    .AddPropertyIfNotNull("prefix", prefix)
                    .AddPropertyIfNotNull("delimiter", delimiter)
                    .AddPropertyIfNotNull("startFileName", startFileName)
                    .ToString());

            using (request)
            {
                HttpResponseMessage responseMessage =
                    await this.SendRequestAsync(request).ConfigureAwait(false);

                using (responseMessage)
                {
                    ListFileNamesResponse response =
                        await responseMessage.Content
                            .TryReadAsJsonAsync<ListFileNamesResponse>()
                            .ConfigureAwait(false);

                    return response;
                }
            }
        }

        /// <summary>
        /// Upload a file to B2 in a single HTTP payload
        /// </summary>
        /// <param name="fileName">The full name of the file (including relative path)</param>
        /// <param name="sha1Hash">The 40-character SHA1 hash of the file's content</param>
        /// <param name="size">The size of the file in bytes</param>
        /// <param name="bucketId">The bucket ID of the bucket where the file will be uploaded</param>
        /// <param name="stream">The <see cref="Stream"/> that exposes the file content</param>
        /// <returns>(async) The file upload response</returns>
        /// <remarks>See https://www.backblaze.com/b2/docs/b2_upload_file.html for additional information</remarks>
        public async Task<BackblazeB2FileUploadResponse> UploadFile(
            string fileName, 
            string sha1Hash,
            long size,
            string bucketId,
            Stream stream)
        {
            // Get the upload information (destination URL and temporary auth token)
            GetUploadUrlResponse uploadUrlResponse = await this.GetUploadUrl(bucketId);

            BackblazeB2FileUploadResponse uploadResponse;

            HttpRequestMessage request = CreateRequestMessage(
                HttpMethod.Post,
                uploadUrlResponse.UploadUrl);

            using (request)
            {
                // Add the authorization header for the temporary authorization token
                request.Headers.TryAddWithoutValidation(
                    "Authorization",
                    uploadUrlResponse.AuthorizationToken);

                // Add the B2 require headers
                request.Headers.Add(Constants.Headers.FileName, fileName);
                request.Headers.Add(Constants.Headers.ContentSha1, sha1Hash);

                request.Content = new DelayedDisposeStreamContent(stream);

                // Set the content type to 'auto' where B2 will determine the content type
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("b2/x-auto");

                HttpResponseMessage responseMessage =
                    await this.SendRequestAsync(request).ConfigureAwait(false);

                using (responseMessage)
                {
                    uploadResponse =
                        await responseMessage.Content.TryReadAsJsonAsync<BackblazeB2FileUploadResponse>();
                }
            }

            return uploadResponse;
        }

        public async Task<StartLargeFileResponse> StartLargeUpload(string bucketId, string filename)
        {
            HttpRequestMessage startLargeFileRequest = null;
            HttpResponseMessage startLargeFileResponse = null;

            StartLargeFileResponse response;

            try
            {
                startLargeFileRequest = this.BuildJsonRequest(
                    Constants.ApiStartLargeFileUrl,
                    HttpMethod.Post,
                    new JsonBuilder()
                        .AddProperty("bucketId", bucketId)
                        .AddProperty("fileName", filename)
                        .AddProperty("contentType", "b2/x-auto")
                        .ToString());

                startLargeFileResponse =
                    await this.SendRequestAsync(startLargeFileRequest).ConfigureAwait(false);

                response = await startLargeFileResponse.Content
                        .TryReadAsJsonAsync<StartLargeFileResponse>()
                        .ConfigureAwait(false);
            }
            finally
            {
                startLargeFileResponse?.Dispose();
                startLargeFileRequest?.Dispose();
            }

            return response;
        }

        public async Task<FinishLargeFileResponse> FinishLargeFile(string fileId, string[] sha1Array)
        {
            HttpRequestMessage finishLargeFileRequest = null;
            HttpResponseMessage finishLargeFileResponse = null;

            FinishLargeFileResponse response;

            try
            {
                finishLargeFileRequest = this.BuildJsonRequest(
                    Constants.ApiFinishLargeFileUrl,
                    HttpMethod.Post,
                    new JsonBuilder()
                        .AddProperty("fileId", fileId)
                        .AddArrayProperty("partSha1Array", sha1Array)
                        .ToString());

                finishLargeFileResponse =
                    await this.SendRequestAsync(finishLargeFileRequest).ConfigureAwait(false);

                response = await finishLargeFileResponse.Content
                    .TryReadAsJsonAsync<FinishLargeFileResponse>()
                    .ConfigureAwait(false);
            }
            finally
            {
                finishLargeFileResponse?.Dispose();
                finishLargeFileRequest?.Dispose();
            }

            return response;
        }

        public async Task<ListLargeUnfinishedFilesResponse> GetUnfinishedLargeFiles(string bucketId)
        {
            HttpRequestMessage listUnfinishedLargeFilesRequest = null;
            HttpResponseMessage listUnfinishedLargeFilesResponse = null;

            ListLargeUnfinishedFilesResponse response;

            try
            {
                listUnfinishedLargeFilesRequest = this.BuildJsonRequest(
                    Constants.ApiListLargeUnfinishedFilesUrl,
                    HttpMethod.Post,
                    new JsonBuilder()
                        .AddProperty("bucketId", bucketId)
                        .ToString());

                listUnfinishedLargeFilesResponse =
                    await this.SendRequestAsync(listUnfinishedLargeFilesRequest).ConfigureAwait(false);

                response = await listUnfinishedLargeFilesResponse.Content
                    .TryReadAsJsonAsync<ListLargeUnfinishedFilesResponse>()
                    .ConfigureAwait(false);
            }
            finally
            {
                listUnfinishedLargeFilesResponse?.Dispose();
                listUnfinishedLargeFilesRequest?.Dispose();
            }

            return response;
        }

        public async Task<CancelLargeFileResponse> CancelLargeFile(string fileId)
        {
            HttpRequestMessage cancelLargeFileRequest = null;
            HttpResponseMessage cancelLargeFileResponse = null;

            CancelLargeFileResponse response;

            try
            {
                cancelLargeFileRequest = this.BuildJsonRequest(
                    Constants.ApiCancelLargeFileUrl,
                    HttpMethod.Post,
                    new JsonBuilder()
                        .AddProperty("fileId", fileId)
                        .ToString());

                cancelLargeFileResponse =
                    await this.SendRequestAsync(cancelLargeFileRequest).ConfigureAwait(false);

                response = await cancelLargeFileResponse.Content
                    .TryReadAsJsonAsync<CancelLargeFileResponse>()
                    .ConfigureAwait(false);
            }
            finally
            {
                cancelLargeFileResponse?.Dispose();
                cancelLargeFileRequest?.Dispose();
            }

            return response;
        }

        public async Task<GetUploadPartUrlResponse> GetUploadPartUrl(string fileId)
        {
            HttpRequestMessage getUploadPartUrlRequest = null;
            HttpResponseMessage getUploadPartUrlResponse = null;

            GetUploadPartUrlResponse response;

            try
            {
                getUploadPartUrlRequest = this.BuildJsonRequest(
                    Constants.ApiGetUploadPartUrl,
                    HttpMethod.Post,
                    new JsonBuilder()
                        .AddProperty("fileId", fileId)
                        .ToString());

                getUploadPartUrlResponse =
                    await this.SendRequestAsync(getUploadPartUrlRequest).ConfigureAwait(false);

                response = await getUploadPartUrlResponse.Content
                        .TryReadAsJsonAsync<GetUploadPartUrlResponse>()
                        .ConfigureAwait(false);
            }
            finally
            {
                getUploadPartUrlResponse?.Dispose();
                getUploadPartUrlRequest?.Dispose();
            }

            return response;
        }

        public async Task<UploadPartResponse> UploadPart(
            string uploadUrl,
            SecureString authorizationToken,
            int partNumber,
            string sha1Hash,
            long size,
            Stream stream)
        {
            UploadPartResponse uploadResponse;

            HttpRequestMessage request = CreateRequestMessage(
                HttpMethod.Post,
                uploadUrl);

            using (request)
            {
                // Add the authorization header for the temporary authorization token
                request.Headers.TryAddWithoutValidation(
                    "Authorization",
                    authorizationToken.GetDecrytped());

                // Add the B2 require headers
                request.Headers.Add(Constants.Headers.PartNumber, partNumber.ToString());
                request.Headers.Add(Constants.Headers.ContentSha1, sha1Hash);

                request.Content = new DelayedDisposeStreamContent(stream);

                HttpResponseMessage responseMessage =
                    await this.SendRequestAsync(request).ConfigureAwait(false);

                using (responseMessage)
                {
                    uploadResponse =
                        await responseMessage.Content.TryReadAsJsonAsync<UploadPartResponse>();
                }
            }

            return uploadResponse;

        }

        private async Task<HttpResponseMessage> SendRequestAsync(
            HttpRequestMessage request)
        {
            try
            {
                return await this.SendRequestAsync(request, this.httpClient).ConfigureAwait(false);
            }
            finally
            {
                request.DisposeCustomContent();
            }
        }

        private async Task<HttpResponseMessage> SendRequestAsync(
            HttpRequestMessage request, 
            HttpClient client)
        {
            LogRequest(request, client.BaseAddress);

            LogApiCallCounter(request);

            HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
            LogResponse(response);

            // Check for token refresh
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                BackblazeErrorResponse errorResponse =
                    await response.Content.TryReadAsJsonAsync<BackblazeErrorResponse>();

                if (errorResponse.Code == Constants.ErrorCodes.ExpiredAuthToken ||
                    // [2018/08/09][ It appears that if the access token is old enough, it will return 
                    // a code of unauthorized instead of expired_auth_token.
                    errorResponse.Code == "unauthorized")
                {
                    // Refresh the auth token
                    await this.AuthorizeAccount();

                    HttpRequestMessage newRequest = await request.Clone().ConfigureAwait(false);

                    newRequest.Headers.Remove("Authorization");
                    newRequest.Headers.TryAddWithoutValidation(
                        "Authorization",
                        this.connectionInfo.AuthorizationToken.GetDecrytped());
                    LogRequest(newRequest, client.BaseAddress);

                    // Dispose of the previous response before creating the new one
                    response.Dispose();

                    LogApiCallCounter(newRequest);

                    response = await client.SendAsync(newRequest).ConfigureAwait(false);
                    LogResponse(response);
                }
            }

            // Any failures (including those from re-issuing after a request) will be handled here
            if (!response.IsSuccessStatusCode)
            {
                // Attempt to read the error information
                BackblazeErrorResponse errorResponse =
                    await response.Content.TryReadAsJsonAsync<BackblazeErrorResponse>();

                if (errorResponse != null)
                {
                    throw new BackblazeB2HttpException(errorResponse);
                }

                throw new BackblazeB2HttpException(
                    "<Failed to read error content>",
                    (int)response.StatusCode,
                    "unknown");
            }

            return response;
        }

        private async Task AuthorizeAccount()
        {
            HttpRequestMessage request = CreateRequestMessage(
                HttpMethod.Get,
                Constants.DefaultApiUrl + Constants.ApiAuthorizeAccountUrl);

            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(
                        this.accountId + ":" + this.applicationKey.GetDecrytped())));

            LogApiCallCounter(request);

            using (HttpResponseMessage response = await this.httpClient.SendAsync(request).ConfigureAwait(false))
            {
                await ThrowIfFatalResponse(response).ConfigureAwait(false);

                JObject responseObject =  
                    await response.Content.ReadAsJObjectAsync().ConfigureAwait(false);

                this.connectionInfo?.Dispose();

                this.connectionInfo = new BackblazeConnectionInfo
                {
                    AuthorizationToken = SecureStringExtensions.FromString(
                        responseObject.Value<string>("authorizationToken")),
                    ApiUrl = responseObject.Value<string>("apiUrl"),
                    DownloadUrl = responseObject.Value<string>("downloadUrl"),
                    RecommendedPartSize = responseObject.Value<int>("recommendedPartSize"),
                    AbsoluteMinimumPartSize = responseObject.Value<int>("absoluteMinimumPartSize"),
                    WhenAcquired = DateTime.UtcNow,
                };

                this.ConnectionInfoChanged?.Invoke(
                    this,
                    new ConnectionInfoChangedEventArgs
                    {
                        AccountId = this.accountId,
                        ConnectionInfo = this.connectionInfo
                    });
            }
        }

        private HttpRequestMessage BuildJsonRequest(
            string urlPart, 
            HttpMethod method,
            string content)
        {
            if (string.IsNullOrWhiteSpace(this.connectionInfo?.ApiUrl))
            {
                throw new Exception("The connection information has not been initialized.");
            }

            HttpRequestMessage request = CreateRequestMessage(
                method,
                this.connectionInfo.ApiUrl + urlPart);

            request.Headers.TryAddWithoutValidation(
                "Authorization",
                this.connectionInfo.AuthorizationToken.GetDecrytped());

            request.Content = new DelayedDisposeStringContent(
                content,
                Encoding.UTF8,
                "application/json");

            return request;
        }

        private static async Task ThrowIfFatalResponse(HttpResponseMessage response)
        {
            if (response.StatusCode == HttpStatusCode.BadRequest ||
                response.StatusCode == HttpStatusCode.Unauthorized ||
                response.StatusCode == HttpStatusCode.Forbidden ||
                (int)response.StatusCode == 429 || // TooManyRequests
                response.StatusCode == HttpStatusCode.InternalServerError ||
                response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                // Attempt to read the error information
                BackblazeErrorResponse errorResponse =
                    await response.Content.TryReadAsJsonAsync<BackblazeErrorResponse>();

                if (errorResponse != null)
                {
                    throw new BackblazeB2HttpException(errorResponse);
                }

                throw new BackblazeB2HttpException(
                    "<Failed to read error content>", 
                    (int)response.StatusCode, 
                    "unknown");
            }
        }

        private HttpRequestMessage CreateRequestMessage(HttpMethod method, string url)
        {
            HttpRequestMessage request = new HttpRequestMessage(method, url);

            request.Headers.Add("UserAgent", this.userAgentString);

            return request;
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

            Logger.Debug("HttpRequest: {0} to {1}", request.Method, uri);

            if (!includeDetail)
            {
                return;
            }

            Logger.Debug("Headers:");

            foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers)
            {
                if (header.Key == "Authorization")
                {
                    Logger.Debug("   {0} = <removed>", header.Key);
                }
                else
                {
                    Logger.Debug("   {0} = {1}", header.Key, header.Value);
                }
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

        private static void LogApiCallCounter(HttpRequestMessage request)
        {
            string lastSegment = request.RequestUri.Segments.Last();

            Pre.Assert(lastSegment.StartsWith("b2_"));

            CounterManager.LogSyncJobCounter(
                Constants.CounterNames.ApiCall,
                1,
                new CounterDimension(Constants.DimensionNames.ApiCallName, lastSegment));
        }
    }

    public class ConnectionInfoChangedEventArgs : EventArgs
    {
        public string AccountId { get; set; }

        public BackblazeConnectionInfo ConnectionInfo { get; set; }
    }
}
