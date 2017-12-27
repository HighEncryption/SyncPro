namespace SyncPro.Adapters.BackblazeB2.DataModel
{
    using System;

    using Newtonsoft.Json;

    public class Bucket
    {
        [JsonProperty("accountId")]
        public string AccountId { get; set; }

        [JsonProperty("bucketId")]
        public string BucketId { get; set; }

        [JsonProperty("bucketName")]
        public string BucketName { get; set; }

        [JsonProperty("bucketType")]
        public string BucketType { get; set; }
    }

    public class File
    {
        
    }
}
