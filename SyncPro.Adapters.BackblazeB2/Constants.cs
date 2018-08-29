namespace SyncPro.Adapters.BackblazeB2
{
    public static class Constants
    {
        public const string DefaultApiUrl = "https://api.backblazeb2.com";
        public const string ApiAuthorizeAccountUrl = "/b2api/v1/b2_authorize_account";
        public const string ApiCreateBucketUrl = "/b2api/v1/b2_create_bucket";
        public const string ApiListBucketsUrl = "/b2api/v1/b2_list_buckets";
        public const string ApiListFileNamesUrl = "/b2api/v1/b2_list_file_names";
        public const string ApiUploadFileUrl = "/b2api/v1/b2_upload_file";
        public const string ApiGetUploadUrl = "/b2api/v1/b2_get_upload_url";
        public const string ApiStartLargeFileUrl = "/b2api/v1/b2_start_large_file";
        public const string ApiFinishLargeFileUrl = "/b2api/v1/b2_finish_large_file";
        public const string ApiGetUploadPartUrl = "/b2api/v1/b2_get_upload_part_url";
        public const string ApiListLargeUnfinishedFilesUrl = "/b2api/v1/b2_list_unfinished_large_files";
        public const string ApiCancelLargeFileUrl = "/b2api/v1/b2_cancel_large_file";

        // Per the Backblaze spec, minimum and maximum part sizes are 5MB and 5GB, respectively
        // See: https://www.backblaze.com/b2/docs/large_files.html
        public const long LimitPartMinimumSize = 0x500000;
        public const long LimitPartDefaultSize = 0x1000000;
        public const long LimitPartMaximumSize = 0x140000000;

        public static class Headers
        {
            public const string FileName = "X-Bz-File-Name";
            public const string ContentSha1 = "X-Bz-Content-Sha1";
            public const string SrcLastModified = "X-Bz-Info-src_last_modified_millis";
            public const string PartNumber = "X-Bz-Part-Number";
        }

        public static class ErrorCodes
        {
            public const string Unauthorized = "unauthorized";
            public const string MissingPhoneNumber = "missing_phone_number";
            public const string ExpiredAuthToken = "expired_auth_token";
        }

        public static class BucketTypes
        {
            public const string Public = "allPublic";
            public const string Private = "allPrivate";
        }

        public static class CounterNames
        {
            public const string ApiCall = "BackblazeAdapterApiCall";
        }

        public static class DimensionNames
        {
            public const string ApiCallName = "ApiCallName";
        }

    }
}
