namespace SyncPro.Adapters.BackblazeB2
{
    using System;
    using System.Security;

    using Newtonsoft.Json;

    using SyncPro.Configuration;
    using SyncPro.Utility;

    public class BackblazeB2AdapterConfiguration : AdapterConfiguration
    {
        public override Guid AdapterTypeId => BackblazeB2Adapter.TargetTypeId;

        public string AccountId { get; set; }

        [JsonConverter(typeof(SecureStringToProtectedDataConverter))]
        public SecureString ApplicationKey { get; set; }

        public BackblazeConnectionInfo ConnectionInfo { get; set; }
    }
}