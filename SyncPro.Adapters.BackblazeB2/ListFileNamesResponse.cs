namespace SyncPro.Adapters.BackblazeB2
{
    using Newtonsoft.Json;

    using SyncPro.Adapters.BackblazeB2.DataModel;

    internal class ListFileNamesResponse
    {
        [JsonProperty("files")]
        public File[] Files { get; set; }

        [JsonProperty("nextFileName")]
        public string NextFileName { get; set; }
    }
}