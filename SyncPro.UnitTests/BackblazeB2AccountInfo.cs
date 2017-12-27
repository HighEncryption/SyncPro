namespace SyncPro.UnitTests
{
    using System.Security;

    using Newtonsoft.Json;

    using SyncPro.Adapters.BackblazeB2;
    using SyncPro.Utility;

    public class BackblazeB2AccountInfo
    {
        public string AccountId { get; set; }

        [JsonConverter(typeof(SecureStringToProtectedDataConverter))]
        public SecureString ApplicationKey { get; set; }

        public BackblazeConnectionInfo ConnectionInfo { get; set; }

        public string BucketId { get; set; }
    }
}