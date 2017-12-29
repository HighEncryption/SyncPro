namespace SyncPro.Adapters.BackblazeB2.DataModel
{
    using System;

    public class BackblazeB2BucketItem : IAdapterItem
    {
        public string Name { get; set; }

        public string UniqueId { get; set; }

        public SyncAdapterItemType ItemType { get; set; }

        public string FullName { get; set; }

        public long Size { get; set; }

        public DateTime CreationTimeUtc { get; set; }

        public DateTime ModifiedTimeUtc { get; set; }

        public byte[] Sha1Hash { get; set; }

        public IAdapterItem Parent { get; set; }

        public AdapterBase Adapter { get; set; }

        public string ErrorMessage { get; set; }
    }
}