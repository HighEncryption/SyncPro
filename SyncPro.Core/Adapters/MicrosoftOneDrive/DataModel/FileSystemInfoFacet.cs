namespace SyncPro.Adapters.MicrosoftOneDrive.DataModel
{
    using System;

    using Newtonsoft.Json;

    /// <summary>
    /// Contains properties that are reported by the device's local file system for the local version of an 
    /// item. This facet can be used to specify the last modified date or created date of the item as it was 
    /// on the local device.
    /// </summary>
    public class FileSystemInfoFacet
    {
        /// <summary>
        /// The UTC date and time the file was created on a client.
        /// </summary>
        [JsonProperty("createdDateTime")]
        public DateTime? CreatedDateTime { get; set; }

        /// <summary>
        /// The UTC date and time the file was last modified on a client.
        /// </summary>
        [JsonProperty("lastModifiedDateTime")]
        public DateTime? LastModifiedDateTime { get; set; }
    }
}