namespace SyncPro.Adapters.BackblazeB2
{
    using System;

    using SyncPro.Configuration;

    public class BackblazeB2AdapterConfiguration : AdapterConfiguration
    {
        public override Guid AdapterTypeId => BackblazeB2Adapter.TargetTypeId;
    }
}