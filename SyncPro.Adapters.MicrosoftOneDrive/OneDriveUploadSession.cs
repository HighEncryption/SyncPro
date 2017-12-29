namespace SyncPro.Adapters.MicrosoftOneDrive
{
    using System;

    using SyncPro.Adapters.MicrosoftOneDrive.DataModel;

    public enum OneDriveFileUploadState
    {
        Undefined,
        NotStarted,
        InProgress,
        Completed,
        Faulted,
        Cancelled,
    }

    public class OneDriveUploadSession
    {
        public string ParentId { get; }

        public string ItemName { get;  }

        public string UploadUrl { get; }

        public DateTime ExpirationDateTime { get; }

        public long Length { get; }

        public OneDriveFileUploadState State { get; internal set; }

        /// <summary>
        /// The item that was returned if the file was uploaded successfully.
        /// </summary>
        public Item Item { get; set; }

        internal OneDriveUploadSession(string parentId, string itemName, string uploadUrl, DateTime expirationDateTime, long length)
        {
            this.ParentId = parentId;
            this.ItemName = itemName;
            this.UploadUrl = uploadUrl;
            this.ExpirationDateTime = expirationDateTime;
            this.Length = length;
        }
    }
}