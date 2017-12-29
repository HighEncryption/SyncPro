namespace SyncPro.Adapters.BackblazeB2.DataModel
{
    using Newtonsoft.Json;

    public class ListLargeUnfinishedFilesResponse
    {
        [JsonProperty("files")]
        public UnfinishedFile[] Files { get; set; }
    }
}