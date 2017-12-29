namespace SyncPro.Adapters.MicrosoftOneDrive.DataModel
{
    using Newtonsoft.Json;

    public class ItemReference
    {
        /// <summary>
        /// Unique identifier for the Drive that contains the item.
        /// </summary>
        [JsonProperty("driveId")]
        public string DriveId { get; set; }

        /// <summary>
        /// Unique identifier for the item.
        /// </summary>
        [JsonProperty("id")]
        public string Id { get; set; }

        /// <summary>
        /// Path that used to navigate to the item.
        /// </summary>
        /// <remarks>
        /// The path value is a OneDrive API path, for example: /drive/root:/Documents/myfile.docx. To retrieve the human-
        /// readable path for a breadcrumb, you can safely ignore everything up to the first : in the path string.
        /// </remarks>
        [JsonProperty("path")]
        public string Path { get; set; }
    }
}