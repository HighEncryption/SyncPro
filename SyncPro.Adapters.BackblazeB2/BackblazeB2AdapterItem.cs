namespace SyncPro.Adapters.BackblazeB2
{
    using System;

    public class BackblazeB2AdapterItem : AdapterItem
    {
        public bool IsBucket { get; set; }

        public BackblazeB2AdapterItem(
            string name,
            IAdapterItem parent,
            AdapterBase adapter,
            SyncAdapterItemType itemType,
            string uniqueId,
            long size,
            DateTime creationTimeUtc,
            DateTime modifiedTimeUtc)
            : base(
                name,
                parent, 
                adapter, 
                itemType, 
                uniqueId, 
                size, 
                creationTimeUtc, 
                modifiedTimeUtc)
        {
        }
    }
}