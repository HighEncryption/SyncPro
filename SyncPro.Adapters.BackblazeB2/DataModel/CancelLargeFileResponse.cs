namespace SyncPro.Adapters.BackblazeB2.DataModel
{
    using Newtonsoft.Json;

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