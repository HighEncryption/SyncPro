namespace SyncPro.Adapters.BackblazeB2.DataModel
{
    using System;

    using Newtonsoft.Json;

    using SyncPro.Utility;

    public class File
    {
        [JsonProperty("fileName")]
        public string FullName { get; set; }

        [JsonProperty("fileId")]
        public string FileId { get; set; }

        [JsonProperty("contentLength")]
        public long ContentLength { get; set; }

        [JsonProperty("contentType")]
        public string ContentType { get; set; }

        [JsonProperty("contentSha1")]
        public string ContentSha1 { get; set; }

        [JsonProperty("fileInfo")]
        public FileInfo FileInfo { get; set; }

        [JsonProperty("action")]
        [JsonConverter(typeof(B2ActionToFileTypeConverter))]
        public bool IsFileType { get;set;}

        [JsonProperty("uploadTimestamp")]
        [JsonConverter(typeof(UnixMillisecondsToDateTimeConverter))]
        public DateTime UploadTimestamp { get; set; }
    }

    public class FileInfo
    {
        [JsonProperty("src_last_modified_millis")]
        [JsonConverter(typeof(UnixMillisecondsToDateTimeConverter))]
        public DateTime LastModified { get; set; }
    }
}