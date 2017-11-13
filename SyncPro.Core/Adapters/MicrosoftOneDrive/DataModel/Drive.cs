namespace SyncPro.Adapters.MicrosoftOneDrive.DataModel
{
    using Newtonsoft.Json;

    public class Drive
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("driveType")]
        public string DriveType { get; set; }

        [JsonProperty("owner")]
        public IdentitySet Owner { get; set; }

        [JsonProperty("quota")]
        public Quota Quota { get; set; }
    }
}