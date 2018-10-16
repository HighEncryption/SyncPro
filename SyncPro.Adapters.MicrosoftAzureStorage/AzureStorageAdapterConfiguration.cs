namespace SyncPro.Adapters.MicrosoftAzureStorage
{
    using System;
    using System.Security;

    using Newtonsoft.Json;

    using SyncPro.Configuration;
    using SyncPro.Utility;

    public class AzureStorageAdapterConfiguration : AdapterConfiguration
    {
        public override Guid AdapterTypeId => AzureStorageAdapter.TargetTypeId;

        public override bool DirectoriesAreUniqueEntities => false;

        public string ContainerName { get; set; }

        public string AccountName { get; set; }

        [JsonConverter(typeof(SecureStringToProtectedDataConverter))]
        public SecureString AccountKey { get; set; }

    }
}