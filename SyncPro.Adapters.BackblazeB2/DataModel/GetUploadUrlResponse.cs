namespace SyncPro.Adapters.BackblazeB2.DataModel
{
    using Newtonsoft.Json;

    public class GetUploadUrlResponse
    {
        [JsonProperty("bucketId")]
        public string BucketId { get; set; }

        [JsonProperty("uploadUrl")]
        public string UploadUrl { get; set; }

        [JsonProperty("authorizationToken")]
        public string AuthorizationToken { get; set; }
    }
}