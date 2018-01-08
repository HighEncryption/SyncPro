namespace SyncPro.Adapters
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public static class AdapterRegistry
    {
        private static readonly List<AdapterRegistration> Registrations;

        static AdapterRegistry()
        {
            Registrations = new List<AdapterRegistration>();
        }

        public static AdapterRegistration GetRegistrationByTypeId(Guid typeId)
        {
            return GetRegistrationByTypeId(typeId, true);
        }

        public static AdapterRegistration GetRegistrationByTypeId(Guid typeId, bool throwOnMissingRegistration)
        {
            var registration = Registrations.FirstOrDefault(r => r.TypeId == typeId);

            if (registration == null)
            {
                throw new Exception("No adapter registration found for type " + typeId);
            }

            return registration;
        }

        public static void RegisterAdapter(
            Guid typeId,
            Type adapterType,
            Type adapterConfigurationType)
        {
            if (Registrations.Any(r => r.TypeId == typeId))
            {
                return;
            }

            Registrations.Add(
                new AdapterRegistration(typeId, adapterType, adapterConfigurationType));
        }
    }
}