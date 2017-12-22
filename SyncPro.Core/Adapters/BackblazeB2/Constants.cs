namespace SyncPro.Adapters.BackblazeB2
{
    public static class Constants
    {
        public const string DefaultApiUrl = "https://api.backblazeb2.com";
        public const string ApiAuthorizeAccountUrl = "/b2api/v1/b2_authorize_account";
        public const string ApiListBucketsUrl = "/b2api/v1/b2_list_buckets";
        public const string ApiListFileNamesUrl = "/b2api/v1/b2_list_file_names";

        public static class ErrorCodes
        {
            public const string Unauthorized = "unauthorized";
            public const string MissingPhoneNumber = "missing_phone_number";
            public const string ExpiredAuthToken = "expired_auth_token";
        }
    }
}
