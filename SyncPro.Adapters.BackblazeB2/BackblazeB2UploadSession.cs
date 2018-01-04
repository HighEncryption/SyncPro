namespace SyncPro.Adapters.BackblazeB2
{
    using System.Collections.Generic;

    using SyncPro.Adapters.BackblazeB2.DataModel;
    using SyncPro.Data;

    public class BackblazeB2UploadSession
    {
        public BackblazeB2UploadSession(SyncEntry entry, long fileSize)
        {
            this.Entry = entry;
            this.FileSize = fileSize;

            this.PartHashes = new Dictionary<int, string>();

            // Per the spec, part numbers start at 1 (not 0)
            // See: https://www.backblaze.com/b2/docs/b2_upload_part.html
            this.CurrentPartNumber = 1;
        }

        public SyncEntry Entry { get; }

        public long FileSize { get; }

        public BackblazeB2FileUploadResponse UploadResponse { get; set; }

        public StartLargeFileResponse StartLargeFileResponse { get; set; }

        public GetUploadPartUrlResponse GetUploadPartUrlResponse { get; set; }

        internal int CurrentPartNumber { get; set; }

        internal Dictionary<int, string> PartHashes { get; }

        internal long BytesUploaded { get; set; }
    }
}