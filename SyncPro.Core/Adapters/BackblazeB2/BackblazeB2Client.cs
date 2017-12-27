namespace SyncPro.Adapters.BackblazeB2
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Security;
    using System.Text;
    using System.Threading.Tasks;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    using SyncPro.Adapters.BackblazeB2.DataModel;
    using SyncPro.Data;
    using SyncPro.Tracing;
    using SyncPro.Utility;

    internal class ListBucketsResponse
    {
        [JsonProperty("buckets")]
        public Bucket[] Buckets { get; set; }
    }

    public class BackblazeB2Client : IDisposable
    {
        private readonly string accountId;

        private readonly SecureString applicationKey;

        private BackblazeConnectionInfo connectionInfo;

        private HttpClient httpClient;

        public event EventHandler<ConnectionInfoChangedEventArgs> ConnectionInfoChanged;

        public BackblazeB2Client(
            string accountId, 
            SecureString applicationKey, 
            BackblazeConnectionInfo connectionInfo)
        {
            this.accountId = accountId;
            this.applicationKey = applicationKey;
            this.connectionInfo = connectionInfo;
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

            HttpRequestMessage request = new HttpRequestMessage(
                HttpMethod.Post,
                uploadUrlResponse.UploadUrl);

            using (request)
            {
                // Add the authorization header for the temporary authorization token
                request.Headers.Add(
                    "Authorization",
                    uploadUrlResponse.AuthorizationToken);

                // Add the B2 require headers
                request.Headers.Add(Constants.Headers.FileName, fileName);
                request.Headers.Add(Constants.Headers.ContentSha1, sha1Hash);

                request.Content = new StreamContent(stream);

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

        public async Task<BackblazeB2UploadPartResponse> UploadPart(
            string uploadUrl,
            SecureString authorizationToken,
            int partNumber,
            string sha1Hash,
            long size,
            Stream stream)
        {
            BackblazeB2UploadPartResponse uploadResponse;

            HttpRequestMessage request = new HttpRequestMessage(
                HttpMethod.Post,
                uploadUrl);

            using (request)
            {
                // Add the authorization header for the temporary authorization token
                request.Headers.Add(
                    "Authorization",
                    authorizationToken.GetDecrytped());

                // Add the B2 require headers
                request.Headers.Add(Constants.Headers.PartNumber, partNumber.ToString());
                request.Headers.Add(Constants.Headers.ContentSha1, sha1Hash);

                request.Content = new StreamContent(stream);

                HttpResponseMessage responseMessage =
                    await this.SendRequestAsync(request).ConfigureAwait(false);

                using (responseMessage)
                {
                    uploadResponse =
                        await responseMessage.Content.TryReadAsJsonAsync<BackblazeB2UploadPartResponse>();
                }
            }

            return uploadResponse;

        }

        private async Task<HttpResponseMessage> SendRequestAsync(
            HttpRequestMessage request)
        {
            return await this.SendRequestAsync(request, this.httpClient).ConfigureAwait(false);
        }

        private async Task<HttpResponseMessage> SendRequestAsync(
            HttpRequestMessage request, 
            HttpClient client)
        {
            LogRequest(request, client.BaseAddress);

            HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
            LogResponse(response);

            // Check for token refresh
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                BackblazeErrorResponse errorResponse =
                    await response.Content.TryReadAsJsonAsync<BackblazeErrorResponse>();

                if (errorResponse.Code == Constants.ErrorCodes.ExpiredAuthToken)
                {
                    // Refresh the auth token
                    await this.AuthorizeAccount();

                    HttpRequestMessage newRequest = await request.Clone().ConfigureAwait(false);

                    request.Headers.Remove("Authorization");
                    request.Headers.Add(
                        "Authorization",
                        this.connectionInfo.AuthorizationToken.GetDecrytped());
                    LogRequest(request, client.BaseAddress);

                    // Dispose of the previous response before creating the new one
                    response.Dispose();

                    response = await client.SendAsync(newRequest).ConfigureAwait(false);
                    LogResponse(response);

                }
            }

            // Any failures (including those from re-issuing after a refresh) will ne handled here
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
            HttpRequestMessage request = new HttpRequestMessage(
                HttpMethod.Get,
                Constants.DefaultApiUrl + Constants.ApiAuthorizeAccountUrl);

            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(
                        this.accountId + ":" + this.applicationKey.GetDecrytped())));

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

            HttpRequestMessage request = new HttpRequestMessage(
                method,
                this.connectionInfo.ApiUrl + urlPart);

            request.Headers.Add(
                "Authorization",
                this.connectionInfo.AuthorizationToken.GetDecrytped());

            request.Content = new StringContent(
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
    }

    public class BackblazeErrorResponse
    {
        [JsonProperty("status")]
        public int Status { get; set; }

        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }

    public class BackblazeConnectionInfo : IDisposable
    {
        [JsonConverter(typeof(SecureStringToProtectedDataConverter))]
        public SecureString AuthorizationToken { get; set; }

        public string ApiUrl { get; set; }

        public string DownloadUrl { get; set; }

        public int RecommendedPartSize { get; set; }

        public int AbsoluteMinimumPartSize { get; set; }

        public void Dispose()
        {
            this.AuthorizationToken?.Dispose();
        }
    }

    public class ConnectionInfoChangedEventArgs : EventArgs
    {
        public string AccountId { get; set; }

        public BackblazeConnectionInfo ConnectionInfo { get; set; }
    }

    public class BackblazeB2UploadSession
    {
        public BackblazeB2UploadSession(SyncEntry entry)
        {
            this.Entry = entry;

            this.PartHashes = new Dictionary<int, string>();

            // Per the spec, part numbers start at 1 (not 0)
            // See: https://www.backblaze.com/b2/docs/b2_upload_part.html
            this.CurrentPartNumber = 1;
        }

        public SyncEntry Entry { get; set; }

        public BackblazeB2FileUploadResponse UploadResponse { get; set; }

        public StartLargeFileResponse StartLargeFileResponse { get; set; }

        public GetUploadPartUrlResponse GetUploadPartUrlResponse { get; set; }

        internal int CurrentPartNumber { get; set; }

        internal Dictionary<int, string> PartHashes { get; }

        internal long BytesUploaded { get; set; }
    }

    public class GetUploadUrlResponse
    {
        [JsonProperty("bucketId")]
        public string BucketId { get; set; }

        [JsonProperty("uploadUrl")]
        public string UploadUrl { get; set; }

        [JsonProperty("authorizationToken")]
        public string AuthorizationToken { get; set; }
    }

    public class StartLargeFileResponse
    {
        [JsonProperty("fileId")]
        public string FileId { get; set; }

        [JsonProperty("fileName")]
        public string FileName { get; set; }

        [JsonProperty("accountId")]
        public string AccountId { get; set; }

        [JsonProperty("bucketId")]
        public string BucketId { get; set; }

        [JsonProperty("contentType")]
        public string ContentType { get; set; }

        [JsonProperty("uploadTimestamp")]
        public long UploadTimestamp { get; set; }
    }

    public class GetUploadPartUrlResponse
    {
        [JsonProperty("fileId")]
        public string FileId { get; set; }

        [JsonProperty("uploadUrl")]
        public string UploadUrl { get; set; }

        [JsonProperty("authorizationToken")]
        [JsonConverter(typeof(SecureStringConverter))]
        public SecureString AuthorizationToken { get; set; }
    }

    public class BackblazeB2UploadPartResponse
    {
        [JsonProperty("fileId")]
        public string FileId { get; set; }

        [JsonProperty("partNumber")]
        public string PartNumber { get; set; }

        [JsonProperty("contentLength")]
        public string ContentLength { get; set; }

        [JsonProperty("contentSha1")]
        public string ContentSha1 { get; set; }
    }

    public class FinishLargeFileResponse
    {
        [JsonProperty("fileId")]
        public string FileId { get; set; }

        [JsonProperty("fileName")]
        public string FileName { get; set; }

        [JsonProperty("accountId")]
        public string AccountId { get; set; }

        [JsonProperty("bucketId")]
        public string BucketId { get; set; }

        [JsonProperty("contentLength")]
        public long ContentLength { get; set; }

        [JsonProperty("contentType")]
        public string ContentType { get; set; }

        [JsonProperty("uploadTimestamp")]
        public long UploadTimestamp { get; set; }
    }

    public class ListLargeUnfinishedFilesResponse
    {
        [JsonProperty("files")]
        public UnfinishedFile[] Files { get; set; }
    }

    public class UnfinishedFile
    {
        [JsonProperty("fileId")]
        public string FileId { get; set; }

        [JsonProperty("fileName")]
        public string FileName { get; set; }

        [JsonProperty("accountId")]
        public string AccountId { get; set; }

        [JsonProperty("bucketId")]
        public string BucketId { get; set; }

        [JsonProperty("contentType")]
        public string ContentType { get; set; }

        [JsonProperty("uploadTimestamp")]
        public long UploadTimestamp { get; set; }
    }

    public class CancelLargeFileResponse
    {
        [JsonProperty("fileId")]
        public string FileId { get; set; }

        [JsonProperty("fileName")]
        public string FileName { get; set; }

        [JsonProperty("accountId")]
        public string AccountId { get; set; }

        [JsonProperty("bucketId")]
        public string BucketId { get; set; }
    }
}
