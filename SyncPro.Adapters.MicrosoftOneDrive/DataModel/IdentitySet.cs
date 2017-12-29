namespace SyncPro.Adapters.MicrosoftOneDrive.DataModel
{
    using Newtonsoft.Json;

    public class IdentitySet
    {
        [JsonProperty("user")]
        public Identity User { get; set; }

        [JsonProperty("application")]
        public Identity Application { get; set; }

        [JsonProperty("device")]
        public Identity Device { get; set; }

        [JsonProperty("organization")]
        public Identity Organization { get; set; }
    }
}