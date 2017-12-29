namespace SyncPro.Adapters.BackblazeB2
{
    using System;
    using System.Security;

    using Newtonsoft.Json;

    using SyncPro.Utility;

    public class BackblazeConnectionInfo : IDisposable
    {
        [JsonConverter(typeof(SecureStringToProtectedDataConverter))]
        public SecureString AuthorizationToken { get; set; }

        public string ApiUrl { get; set; }

        public string DownloadUrl { get; set; }

        public int RecommendedPartSize { get; set; }

        public int AbsoluteMinimumPartSize { get; set; }

        public DateTime WhenAcquired { get; set; }

        public void Dispose()
        {
            this.AuthorizationToken?.Dispose();
        }
    }
}