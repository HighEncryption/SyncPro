namespace SyncPro.Adapters.MicrosoftAzureStorage
{
    using System;

    using SyncPro.Adapters.MicrosoftAzureStorage.DataModel;

    public class AzureStorageAdapterItem : AdapterItem
    {
        public bool IsContainer { get; set; }

        public AzureStorageAdapterItem(
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