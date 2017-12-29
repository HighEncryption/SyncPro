namespace SyncPro.Adapters.MicrosoftOneDrive.DataModel
{
    using Newtonsoft.Json;

    public class UserEmails
    {
        [JsonProperty("preferred", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Preferred { get; set; }

        [JsonProperty("account", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Account { get; set; }

        [JsonProperty("personal", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Personal { get; set; }

        [JsonProperty("business", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Business { get; set; }

    }
}