namespace SyncPro.Adapters.MicrosoftOneDrive.DataModel
{
    using System;

    using Newtonsoft.Json;

    public class Item
    {
        /// <summary>
        /// The unique identifier of the item within the Drive. Read-only.
        /// </summary>
        [JsonProperty("id")]
        public string Id { get; set; }

        /// <summary>
        /// Identity of the user, device, and application which created the item. Read-only.
        /// </summary>
        [JsonProperty("createdBy")]
        public IdentitySet CreatedBy { get; set; }

        /// <summary>
        /// Date and time of item creation. Read-only.
        /// </summary>
        [JsonProperty("createdDateTime")]
        public DateTime CreatedDateTime { get; set; }

        /// <summary>
        /// An eTag for the content of the item. This eTag is not changed if only the metadata is changed. Note This property is 
        /// not returned if the Item is a folder. Read-only.
        /// </summary>
        [JsonProperty("cTag")]
        public string CTag { get; set; }

        /// <summary>
        /// Provide a user-visible description of the item. Read-write.
        /// </summary>
        [JsonProperty("description")]
        public string Description { get; set; }

        /// <summary>
        /// eTag for the entire item (metadata + content). Read-only.
        /// </summary>
        /// <remarks>
        /// The eTag and cTag properties work differently on containers (folders). The cTag value is modified when content or 
        /// metadata of any descendant of the folder is changed. The eTag value is only modified when the folder's properties 
        /// are changed, except for properties that are derived from descendants (like childCount or lastModifiedDateTime).
        /// </remarks>
        [JsonProperty("eTag")]
        public string ETag { get; set; }

        [JsonProperty("file")]
        public FileFacet File { get; set; }

        [JsonProperty("fileSystemInfo")]
        public FileSystemInfoFacet FileSystemInfo { get; set; }

        [JsonProperty("folder")]
        public FolderFacet Folder { get; set; }

        /// <summary>
        /// Identity of the user, device, and application which last modified the item. Read-only.
        /// </summary>
        [JsonProperty("lastModifiedBy")]
        public IdentitySet LastModifiedBy { get; set; }

        /// <summary>
        /// Date and time the item was last modified. Read-only.
        /// </summary>
        [JsonProperty("lastModifiedDateTime")]
        public DateTime? LastModifiedDateTime { get; set; }

        /// <summary>
        /// The name of the item (filename and extension). Read-write.
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>
        /// Parent information, if the item has a parent. Read-write.
        /// </summary>
        [JsonProperty("parentReference")]
        public ItemReference ParentReference { get; set; }

        /// <summary>
        /// Size of the item in bytes. Read-only.
        /// </summary>
        [JsonProperty("size")]
        public long Size { get; set; }

        [JsonProperty("deleted")]
        public DeletedFacet Deleted { get; set; }
    }
}