namespace SyncPro.Adapters.BackblazeB2.DataModel
{
    using System.Security;

    using Newtonsoft.Json;

    using SyncPro.Utility;

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
}