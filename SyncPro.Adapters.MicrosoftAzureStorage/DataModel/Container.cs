namespace SyncPro.Adapters.MicrosoftAzureStorage.DataModel
{
    using System;

    public enum LeaseStatus
    {
        Undefined,
        Locked,
        Unlocked,
    }

    public enum AccessTierType
    {
        Undefined,
        Hot,
        Cool,
        Archive
    }

    public class Container
    {
        public string Name { get; set; }

        public DateTime LastModified { get; set; }

        public string ETag { get; set; }

        public LeaseStatus LeaseStatus { get; set; }

        public string LeastState { get; set; }

        internal static Container FromInternalResult(Internal.Container c)
        {
            DateTime lastModified = DateTime.Parse(c.Properties.LastModified);

            LeaseStatus leaseStatus;
            Enum.TryParse(c.Properties.LeaseStatus, true, out leaseStatus);

            return new Container
            {
                Name = c.Name,
                LastModified = lastModified,
                ETag = c.Properties.Etag.Trim('\"'),
                LeaseStatus = leaseStatus
            };
        }
    }

    public abstract class ContainerItem
    {
        public string Name { get; set; }
    }

    public class BlobPrefix : ContainerItem
    {
        internal static BlobPrefix FromInternalResult(Internal.BlobPrefix b)
        {
            return new BlobPrefix
            {
                Name = b.Name,
            };
        }
    }

    public class Blob : ContainerItem
    {
        public DateTime Created { get; set; }

        public DateTime LastModified { get; set; }

        public string ETag { get; set; }

        public long Length { get; set; }

        public string ContentType { get; set; }

        public byte[] MD5 { get; set; }

        public AccessTierType AccessTier { get; set; }

        public bool AccessTierInferred { get; set; }

        public string LeaseStatus { get; set; }

        public string LeastState { get; set; }

        internal static Blob FromInternalResult(Internal.Blob b)
        {
            // The Last-Modified string will have a value similar to "Mon, 08 Oct 2018 23:09:38 GMT"
            DateTime created = DateTime.Parse(b.Properties.CreationTime);
            DateTime lastModified = DateTime.Parse(b.Properties.LastModified);
            byte[] md5 = Convert.FromBase64String(b.Properties.ContentMD5);

            AccessTierType tier = (AccessTierType)Enum.Parse(
                typeof(AccessTierType), 
                b.Properties.AccessTier, 
                true);

            return new Blob
            {
                Name = b.Name,
                Created = created,
                LastModified = lastModified,
                ETag = b.Properties.Etag.Trim('\"'),
                Length = b.Properties.ContentLength,
                ContentType = b.Properties.ContentType,
                MD5 = md5,
                AccessTier = tier,
                AccessTierInferred = b.Properties.AccessTierInferred
            };
        }
    }


}
