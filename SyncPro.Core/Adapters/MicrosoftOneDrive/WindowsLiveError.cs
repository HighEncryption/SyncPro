namespace SyncPro.Adapters.MicrosoftOneDrive
{
    using Newtonsoft.Json;

    public class WindowsLiveError
    {
        [JsonProperty("error")]
        public string Error { get; set; }

        [JsonProperty("error_description")]
        public string ErrorDescription { get; set; }
    }
}