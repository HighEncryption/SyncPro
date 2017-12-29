namespace SyncPro.Adapters.BackblazeB2.DataModel
{
    using Newtonsoft.Json;

    public class UploadPartResponse
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
}