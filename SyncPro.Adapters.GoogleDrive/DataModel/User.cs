namespace SyncPro.Adapters.GoogleDrive.DataModel
{
    using Newtonsoft.Json;

    public class User
    {
        [JsonProperty("id", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Id { get; set; }

        [JsonProperty("email", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Email { get; set; }

        [JsonProperty("verified_email", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool VerifiedEmail { get; set; }

        [JsonProperty("name", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Name { get; set; }

        [JsonProperty("given_name", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string GivenName { get; set; }

        [JsonProperty("family_name", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string FamilyName { get; set; }

        [JsonProperty("link", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Link { get; set; }

        [JsonProperty("picture", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Picture { get; set; }

        [JsonProperty("gender", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Gender { get; set; }

        [JsonProperty("locale", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Locale { get; set; }
    }
}