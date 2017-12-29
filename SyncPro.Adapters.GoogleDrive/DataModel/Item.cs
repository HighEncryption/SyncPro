namespace SyncPro.Adapters.GoogleDrive.DataModel
{
    using System;

    using Newtonsoft.Json;

    public class Item
    {
        /// <summary>
        /// The ID of the file.
        /// </summary>
        [JsonProperty("id")]
        public string Id { get; set; }

        /// <summary>
        /// The name of the file.
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>
        /// The MIME type of the file.
        /// </summary>
        [JsonProperty("mimeType")]
        public string MimeType { get; set; }

        /// <summary>
        /// Whether the item has been deleted.
        /// </summary>
        [JsonProperty("trashed")]
        public bool Trashed { get; set; }

        /// <summary>
        /// The Date/Time when the item was created.
        /// </summary>
        [JsonProperty("createdTime")]
        public DateTime CreatedTime { get; set; }

        /// <summary>
        /// The Date/Time when the item was last modified
        /// </summary>
        [JsonProperty("modifiedTime")]
        public DateTime ModifiedTime { get; set; }

        /// <summary>
        /// The size of the item
        /// </summary>
        [JsonProperty("size")]
        public long Size { get; set; }

        /// <summary>
        /// The MD5 checksum of the item's contents.
        /// </summary>
        [JsonProperty("md5Checksum")]
        public string Md5Checksum{ get; set; }

        public bool IsFolder => this.MimeType == Constants.MimeTypes.Folder;
    }


    public class ItemList
    {
        [JsonProperty("files")]
        public Item[] Files { get; set; }
    }

}