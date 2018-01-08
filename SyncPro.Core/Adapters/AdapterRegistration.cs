namespace SyncPro.Adapters
{
    using System;

    public class AdapterRegistration
    {
        internal AdapterRegistration(
            Guid typeId,
            Type adapterType,
            Type adapterConfigurationType)
        {
            this.TypeId = typeId;
            this.AdapterType = adapterType;
            this.AdapterConfigurationType = adapterConfigurationType;
        }

        public Guid TypeId { get; }

        public Type AdapterType { get; }

        public Type AdapterConfigurationType { get; }
    }
}