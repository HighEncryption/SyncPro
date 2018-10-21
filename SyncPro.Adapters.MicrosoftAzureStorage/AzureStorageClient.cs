namespace SyncPro.Adapters.MicrosoftAzureStorage
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Runtime.Serialization;
    using System.Security;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using System.Xml;

    using SyncPro.Adapters.MicrosoftAzureStorage.DataModel.Internal;
    using SyncPro.Counters;
    using SyncPro.Tracing;
    using SyncPro.Utility;

    using Container = SyncPro.Adapters.MicrosoftAzureStorage.DataModel.Container;
    using ContainerItem = SyncPro.Adapters.MicrosoftAzureStorage.DataModel.ContainerItem;
    using Blob = SyncPro.Adapters.MicrosoftAzureStorage.DataModel.Blob;
    using BlobPrefix = SyncPro.Adapters.MicrosoftAzureStorage.DataModel.BlobPrefix;

    public class AzureStorageClient : IDisposable
    {
        private const string ApiVersion = "2017-11-09"; // "2016-05-31";

        private readonly string accountName;
        private readonly SecureString accountKey;

        private HttpClient httpClient;

        public AzureStorageClient(
            string accountName,
            SecureString accountKey)
        {
            this.accountName = accountName;
            this.accountKey = accountKey;

            this.httpClient = new HttpClient();
        }

        public void Dispose()
        {
            Dispose(true);
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

        public async Task<IList<Container>> ListContainersAsync()
        {
            List<Container> containers = new List<Container>();
            string nextMarker = null;

            while (true)
            {
                Dictionary<string, string> queryParams = new Dictionary<string, string>()
                {
                    {"comp", "list"}
                };

                queryParams.AddIfValueNotNullOrWhitespace("marker", nextMarker);

                HttpRequestMessage request = BuildRequest(
                    null,
                    queryParams.ToQueryParameters(),
                    HttpMethod.Get);

                using (request)
                {
                    HttpResponseMessage responseMessage =
                        await SendRequestAsync(request).ConfigureAwait(false);

                    using (responseMessage)
                    {
                        EnumerationResults results =
                            await responseMessage.Content.ReadAsXmlObjectsync<EnumerationResults>().ConfigureAwait(false);

                        // Convert from the XML-based result object into our portable type
                        containers.AddRange(results.Containers.Select(Container.FromInternalResult));

                        // TODO: Need to handle the case when there is a 
                        if (string.IsNullOrWhiteSpace(results.NextMarker))
                        {
                            return containers;
                        }

                        nextMarker = results.NextMarker;
                    }
                }
            }
        }

        public async Task<IList<ContainerItem>> ListBlobsAsync(string containerName, string delimiter, string prefix)
        {
            List<ContainerItem> containerItems = new List<ContainerItem>();
            string nextMarker = null;

            while (true)
            {
                Dictionary<string, string> queryParams =
                    new Dictionary<string, string>
                    {
                        {"restype", "container"}, 
                        {"comp", "list"}
                    };

                queryParams.AddIfValueNotNullOrWhitespace("delimiter", delimiter);
                queryParams.AddIfValueNotNullOrWhitespace("prefix", prefix);
                queryParams.AddIfValueNotNullOrWhitespace("marker", nextMarker);

                HttpRequestMessage request = BuildRequest(
                    containerName,
                    queryParams.ToQueryParameters(),
                    HttpMethod.Get);

                using (request)
                {
                    HttpResponseMessage responseMessage =
                        await SendRequestAsync(request).ConfigureAwait(false);

                    using (responseMessage)
                    {
                        EnumerationResults results =
                            await responseMessage.Content.ReadAsXmlObjectsync<EnumerationResults>()
                                .ConfigureAwait(false);

                        // Aggregate the blobs returned in the response
                        // Convert Internal.BlobPrefix to BlobPrefix and add to result
                        containerItems.AddRange(results.Blobs.OfType<DataModel.Internal.BlobPrefix>().Select(BlobPrefix.FromInternalResult));

                        // Convert Internal.Blob to Blob and add to result
                        containerItems.AddRange(results.Blobs.OfType<DataModel.Internal.Blob>().Select(Blob.FromInternalResult));

                        if (string.IsNullOrWhiteSpace(results.NextMarker))
                        {
                            return containerItems;
                        }

                        nextMarker = results.NextMarker;
                    }
                }
            }
        }

        public async Task<HttpResponseMessage> GetBlobRangeAsync(
            string containerName, 
            string blobName, 
            long startByte, 
            long endByte)
        {
            HttpRequestMessage request = BuildRequest(
                containerName,
                null,
                HttpMethod.Get);

            request.Headers.TryAddWithoutValidation(
                "x-ms-range", 
                string.Format("bytes {0}-{1}", startByte, endByte));

            using (request)
            {
                return await SendRequestAsync(request).ConfigureAwait(false);
            }
        }

        public async Task<HttpResponseMessage> GetBlobAsync(
            string containerName, 
            string blobName)
        {
            HttpRequestMessage request = BuildRequest(
                containerName + "/" + blobName,
                null,
                HttpMethod.Get);

            using (request)
            {
                return await SendRequestAsync(request, "GetBlob").ConfigureAwait(false);
            }
        }

        public async Task<HttpResponseMessage> PutBlobAsync(
            string containerName, 
            string blobName,
            byte[] data,
            byte[] md5)
        {
            HttpRequestMessage request = BuildRequest(
                containerName + "/" + blobName,
                null,
                HttpMethod.Put);

            request.Headers.TryAddWithoutValidation(
                "x-ms-blob-type",
                "BlockBlob");

            using (request)
            {
                request.Content = new ByteArrayContent(data);
                request.Content.Headers.ContentMD5 = md5;
                return await SendRequestAsync(request, "PutBlob").ConfigureAwait(false);
            }
        }

        public async Task<HttpResponseMessage> PutBlockAsync(
            string containerName, 
            string blobName,
            string blockId,
            byte[] data,
            byte[] md5)
        {
            Dictionary<string, string> queryParams =
                new Dictionary<string, string>
                {
                    {"comp", "block"},
                    {"blockid", blockId}
                };

            HttpRequestMessage request = BuildRequest(
                containerName + "/" + blobName,
                queryParams.ToQueryParameters(),
                HttpMethod.Put);

            request.Headers.TryAddWithoutValidation(
                "Content-MD5",
                Convert.ToBase64String(md5));

            using (request)
            {
                request.Content = new ByteArrayContent(data);
                return await SendRequestAsync(request).ConfigureAwait(false);
            }
        }

        public async Task<HttpResponseMessage> PutBlockListAsync(
            string containerName, 
            string blobName,
            List<string> blockIDs)
        {
            Dictionary<string, string> queryParams =
                new Dictionary<string, string>
                {
                    {"comp", "blocklist"},
                };

            // Build the XML content that we will send as the body of the request
            XmlDocument xmlDoc = new XmlDocument();
            XmlElement blockListElement = (XmlElement) xmlDoc.AppendChild(
                xmlDoc.CreateElement("BlockList"));

            foreach (string blockID in blockIDs)
            {
                XmlElement blockIdElement = (XmlElement) blockListElement.AppendChild(
                    xmlDoc.CreateElement("Latest"));

                blockIdElement.InnerText = blockID;
            }

            StringBuilder stringBuilder = new StringBuilder();
            using (StringWriter sw = new StringWriter(stringBuilder))
            {
                xmlDoc.Save(sw);
            }

            HttpRequestMessage request = BuildRequest(
                containerName + "/" + blobName,
                queryParams.ToQueryParameters(),
                HttpMethod.Put);

            using (request)
            {
                request.Content = new StringContent(stringBuilder.ToString());
                return await SendRequestAsync(request).ConfigureAwait(false);
            }
        }

        private async Task<HttpResponseMessage> SendRequestAsync(
            HttpRequestMessage request,
            string counterOpName = null,
            bool addAuthenticationHeader = true)
        {
            if (addAuthenticationHeader)
            {
                string md5 = null;
                if (request.Content?.Headers.ContentMD5 != null)
                {
                    md5 = Convert.ToBase64String(request.Content.Headers.ContentMD5);
                }

                string sharedKey = GetAuthorizationSharedKey(
                    this.accountName,
                    this.accountKey,
                    DateTime.UtcNow, 
                    request,
                    md5:md5);

                request.Headers.Authorization = new AuthenticationHeaderValue("SharedKey", sharedKey);
            }

            LogRequest(request, this.httpClient.BaseAddress);

            LogApiCallCounter(request, counterOpName);

            HttpResponseMessage response = await this.httpClient.SendAsync(request).ConfigureAwait(false);

            LogResponse(response);

            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            AzureStorageErrorResponse errorResponse;

            try
            {
                errorResponse = ReadErrorResponse(content);
            }
            catch (Exception exception)
            {
                throw new AzureStorageHttpException(
                    "Failed to read error message in response",
                    exception);
            }

            throw new AzureStorageHttpException(errorResponse);
        }

        private HttpRequestMessage BuildRequest(
            string path, 
            string query, 
            HttpMethod method)
        {
            DateTime timestamp = DateTime.UtcNow;

            // Build the complete URI
            UriBuilder builder = new UriBuilder(
                string.Format("https://{0}.blob.core.windows.net", this.accountName));

            if (!string.IsNullOrWhiteSpace(path))
            {
                builder.Path = path;
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                builder.Query = query;
            }

            string requestUri = builder.ToString();

            // Build the request message 
            HttpRequestMessage requestMessage = new HttpRequestMessage(method, requestUri);

            // Add the required header to the call
            requestMessage.Headers.TryAddWithoutValidation("x-ms-version", ApiVersion);
            requestMessage.Headers.TryAddWithoutValidation("x-ms-date", timestamp.ToString("R"));

            return requestMessage;
        }

        private static string GetCanonicalizedHeaders(HttpRequestMessage httpRequestMessage)
        {
            var headers = from kvp in httpRequestMessage.Headers
                where kvp.Key.StartsWith("x-ms-", StringComparison.OrdinalIgnoreCase)
                orderby kvp.Key
                select new { Key = kvp.Key.ToLowerInvariant(), kvp.Value };

            StringBuilder sb = new StringBuilder();

            // Create the string in the right format; this is what makes the headers "canonicalized" --
            //   it means put in a standard format. http://en.wikipedia.org/wiki/Canonicalization
            foreach (var kvp in headers)
            {
                StringBuilder headerBuilder = new StringBuilder(kvp.Key);
                char separator = ':';

                // Get the value for each header, strip out \r\n if found, then append it with the key.
                foreach (string headerValues in kvp.Value)
                {
                    string trimmedValue = headerValues.TrimStart().Replace("\r\n", string.Empty);
                    headerBuilder.Append(separator).Append(trimmedValue);

                    // Set this to a comma; this will only be used 
                    //   if there are multiple values for one of the headers.
                    separator = ',';
                }
                sb.Append(headerBuilder).Append("\n");
            }
            return sb.ToString();
        }     

        private static string GetCanonicalizedResource(Uri address, string storageAccountName)
        {
            // The absolute path will be "/" because for we're getting a list of containers.
            StringBuilder sb = new StringBuilder("/").Append(storageAccountName).Append(address.AbsolutePath);

            // Address.Query is the resource, such as "?comp=list".
            // This ends up with a NameValueCollection with 1 entry having key=comp, value=list.
            // It will have more entries if you have more query parameters.
            UriBuilder uri = new UriBuilder(address);
            var elems = uri.Query.TrimStart('?').Split('&');
            Dictionary<string, string> dict = new Dictionary<string, string>();
            foreach (string elem in elems.Where(e => !string.IsNullOrWhiteSpace(e)))
            {
                string[] parts = elem.Split('=');
                dict.Add(parts[0], parts[1]);
            }

            foreach (var item in dict.Keys.OrderBy(k => k))
            {
                sb.Append('\n').Append(item).Append(':').Append(dict[item]);
            }

            return sb.ToString();
        }

        internal static string GetAuthorizationSharedKey(
            string storageAccountName, 
            SecureString storageAccountKey, 
            DateTime now,
            HttpRequestMessage httpRequestMessage, 
            string ifMatch = "", 
            string md5 = "")
        {
            // This is the raw representation of the message signature.
            string messageSignature = string.Format("{0}\n\n\n{1}\n{5}\n\n\n\n{2}\n\n\n\n{3}{4}",
                httpRequestMessage.Method,
                httpRequestMessage.Method == HttpMethod.Get || httpRequestMessage.Method == HttpMethod.Head
                    ? string.Empty
                    : httpRequestMessage.Content.Headers.ContentLength.ToString(),
                ifMatch,
                GetCanonicalizedHeaders(httpRequestMessage),
                GetCanonicalizedResource(httpRequestMessage.RequestUri, storageAccountName),
                md5);

            // Now turn it into a byte array.
            byte[] signatureBytes = Encoding.UTF8.GetBytes(messageSignature);

            // Create the HMACSHA256 version of the storage key.
            using (HMACSHA256 sha256 = new HMACSHA256(Convert.FromBase64String(storageAccountKey.GetDecrytped())))
            {
                // Compute the hash of the SignatureBytes and convert it to a base64 string.
                string signature = Convert.ToBase64String(sha256.ComputeHash(signatureBytes));

                // This is the actual header that will be added to the list of request headers.
                return string.Format("{0}:{1}", storageAccountName, signature);
            }
        }

        private static AzureStorageErrorResponse ReadErrorResponse(string content)
        {
            AzureStorageErrorResponse response = new AzureStorageErrorResponse();

            using (XmlReader reader = XmlReader.Create(new StringReader(content)))
            {
                string currentNodeName = null;
                while (reader.Read())
                {
                    if (currentNodeName != null)
                    {
                        if (string.Equals(currentNodeName, "Code", StringComparison.OrdinalIgnoreCase))
                        {
                            response.Code = reader.Value;
                        }
                        else if (string.Equals(currentNodeName, "Message", StringComparison.OrdinalIgnoreCase))
                        {
                            response.Message = reader.Value;
                        }
                        else
                        {
                            response.Details[currentNodeName] = reader.Value;
                        }

                        currentNodeName = null;
                        continue;
                    }

                    if (reader.NodeType == XmlNodeType.Element && reader.IsStartElement() && reader.Name != "Error")
                    {
                        currentNodeName = reader.Name;
                    }
                }
            }

            return response;
        }

        #region Logging Methods

        private static void LogApiCallCounter(HttpRequestMessage request, string opName)
        {
            if (string.IsNullOrWhiteSpace(opName))
            {
                // OpName was not provided, so try to figure it out
                if (request.RequestUri.Query.Contains("comp=list") &&
                    request.Method == HttpMethod.Get)
                {
                    opName = "ListContainers";
                }
            }

            if (opName == null)
            {
                string message = string.Format(
                    "Failed to determine counter op name from Uri {0}",
                    request.RequestUri);

#if DEBUG
                throw new Exception(message);
#else
                Logger.Warning(message);
                return;
#endif
            }

            CounterManager.LogSyncJobCounter(
                Constants.CounterNames.ApiCall,
                1,
                new CounterDimension(Constants.DimensionNames.ApiCallName, opName));
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

        #endregion
    }

    public static class Constants
    {
        public static class CounterNames
        {
            public const string ApiCall = "BackblazeAdapter/ApiCall";
        }

        public static class DimensionNames
        {
            public const string ApiCallName = "ApiCallName";
        }
    }

    public class AzureStorageErrorResponse
    {
        public string Code { get; set; }

        public string Message { get; set; }

        public Dictionary<string, string> Details { get; }

        public AzureStorageErrorResponse()
        {
            this.Details = new Dictionary<string, string>();
        }
    }

    [Serializable]
    public class AzureStorageHttpException : Exception
    {
        public string Code { get; set; }

        public string ErrorMessage { get; set; }

        public AzureStorageHttpException()
        {
        }

        public AzureStorageHttpException(
            string message,
            Exception innerException)
            : base(message, innerException)
        {
        }

        public AzureStorageHttpException(
            string message,
            string code,
            string errorMessage) 
            : base(message)
        {
            this.ErrorMessage = errorMessage;
            this.Code = code;
        }

        protected AzureStorageHttpException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }

        public AzureStorageHttpException(AzureStorageErrorResponse error)
            : this(error.Message, error.Code, error.Message)
        {
        }
    }
}