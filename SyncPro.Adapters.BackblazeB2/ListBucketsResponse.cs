namespace SyncPro.Adapters.BackblazeB2
{
    using Newtonsoft.Json;

    using SyncPro.Adapters.BackblazeB2.DataModel;

    internal class ListBucketsResponse
    {
        [JsonProperty("buckets")]
        public Bucket[] Buckets { get; set; }
    }
}