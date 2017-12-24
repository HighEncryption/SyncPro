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

        // Per the Backblaze spec, minimum and maximum part sizes are 5MB and 5GB, respectively
        // See: https://www.backblaze.com/b2/docs/large_files.html
        public const long LimitPartMinimumSize = 0x500000;
        public const long LimitPartMaximumSize = 0x140000000;

        public const string HexDigitsAtEnd = "hex_digits_at_end";

        public static class Headers
        {
            public const string FileName = "X-Bz-File-Name";
            public const string ContentSha1 = "X-Bz-Content-Sha1";
            public const string SrcLastModified = "X-Bz-Info-src_last_modified_millis";
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
    }
}
