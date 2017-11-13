namespace SyncPro.Adapters.MicrosoftOneDrive.DataModel
{
    using Newtonsoft.Json;

    public class UserProfile
    {
        [JsonProperty("id", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Id { get; set; }

        [JsonProperty("name", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Name { get; set; }

        [JsonProperty("first_name", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string FirstName { get; set; }

        [JsonProperty("last_name", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string LastName { get; set; }

        [JsonProperty("gender", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Gender { get; set; }

        [JsonProperty("emails", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public UserEmails Emails { get; set; }

        [JsonProperty("locale", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Locale { get; set; }
    }
}