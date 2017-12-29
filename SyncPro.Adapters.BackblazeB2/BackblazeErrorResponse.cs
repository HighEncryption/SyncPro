namespace SyncPro.Adapters.BackblazeB2
{
    using Newtonsoft.Json;

    public class BackblazeErrorResponse
    {
        [JsonProperty("status")]
        public int Status { get; set; }

        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }
}