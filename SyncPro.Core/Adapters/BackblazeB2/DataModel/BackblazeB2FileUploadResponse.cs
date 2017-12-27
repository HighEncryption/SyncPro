namespace SyncPro.Adapters.BackblazeB2.DataModel
{
    using Newtonsoft.Json;

    public class BackblazeB2FileUploadResponse
    {
        /// <summary>
        /// The unique identifier for this version of this file.
        /// </summary>
        [JsonProperty("fileId")]
        public string FileId { get; set; }

        /// <summary>
        /// The name of this file,
        /// </summary>
        [JsonProperty("fileName")]
        public string FileName { get; set; }

        /// <summary>
        /// The Backblaze account ID
        /// </summary>
        [JsonProperty("accountId")]
        public string AccountId { get; set; }

        /// <summary>
        /// The ID of the bucket that the file is in
        /// </summary>
        [JsonProperty("bucketId")]
        public string BucketId { get; set; }

        /// <summary>
        /// The number of bytes stored in the file.
        /// </summary>
        [JsonProperty("contentLength")]
        public long ContentLength { get; set; }

        /// <summary>
        /// The SHA1 of the bytes stored in the file.
        /// </summary>
        [JsonProperty("contentSha1")]
        public string ContentSha1 { get; set; }

        /// <summary>
        /// The MIME type of the file.
        /// </summary>
        [JsonProperty("contentType")]
        public string ContentType { get; set; }

        /// <summary>
        ///  The UTC time when this file was uploaded. It is a base 10 number of milliseconds since midnight, 
        /// January 1, 1970 UTC. 
        /// </summary>
        [JsonProperty("uploadTimestamp")]
        public long UploadTimestamp { get; set; }
    }
}