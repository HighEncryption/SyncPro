namespace SyncPro.Adapters.BackblazeB2.DataModel
{
    using Newtonsoft.Json;

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
}