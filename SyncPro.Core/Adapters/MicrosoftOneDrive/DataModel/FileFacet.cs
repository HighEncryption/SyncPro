namespace SyncPro.Adapters.MicrosoftOneDrive.DataModel
{
    using Newtonsoft.Json;

    public class FileFacet
    {
        [JsonProperty("mimeType")]
        public string MimeType { get; set; }

        [JsonProperty("hashes")]
        public HashesFacet Hashes { get; set; }
    }
}