namespace SyncPro.Adapters.MicrosoftAzureStorage.DataModel.Internal
{
    using System.Xml.Serialization;

    public class BlobProperties
    {
        public string Etag { get; set; }

        [XmlElement("Last-Modified")]
        public string LastModified { get; set; }

        [XmlElement("Creation-Time")]
        public string CreationTime { get; set; }

        [XmlElement("Content-Length")]
        public long ContentLength { get; set; }

        [XmlElement("Content-Type")]
        public string ContentType { get; set; }

        [XmlElement("Content-MD5")]
        public string ContentMD5 { get; set; }

        [XmlElement("Lease-Status")]
        public string LeaseStatus { get; set; }

        [XmlElement("AccessTier")]
        public string AccessTier { get; set; }

        [XmlElement("AccessTierInferred")]
        public bool AccessTierInferred { get; set; }
    }

    public class Container
    {
        public string Name { get; set; }

        public BlobProperties Properties { get; set; }
    }

    public class BlobOrBlobPrefix
    {
        public string Name { get; set; }
    }

    public class Blob : BlobOrBlobPrefix
    {
        public BlobProperties Properties { get; set; }
    }

    public class BlobPrefix : BlobOrBlobPrefix
    {
    }

    public class EnumerationResults
    {
        public int MaxResults { get; set; }

        public Container[] Containers { get; set; }

        [XmlArrayItem("Blob", Type=typeof(Blob))]
        [XmlArrayItem("BlobPrefix", Type=typeof(BlobPrefix))]
        public BlobOrBlobPrefix[] Blobs { get; set; }

        public string NextMarker { get; set; }
    }
}
