namespace SyncPro.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    //public static class AdapterRegistry
    //{
    //    private static readonly List<AdapterRegistration> Registrations;

    //    static AdapterRegistry()
    //    {
    //        Registrations = new List<AdapterRegistration>();
    //    }

    //    public static AdapterRegistration GetRegistrationByTypeId(Guid typeId)
    //    {
    //        AdapterRegistration registration = Registrations.FirstOrDefault(r => r.TypeId == typeId);

    //        if (registration == null)
    //        {
    //            throw new Exception("No adapter registration found with TypeId " + typeId);
    //        }

    //        return registration;
    //    }

    //    public static void RegisterAdapter(
    //        Guid typeId,
    //        Type adapterType,
    //        Type adapterConfigurationType)
    //    {
    //        if (Registrations.Any(r => r.TypeId == typeId))
    //        {
    //            return;
    //        }

    //        Registrations.Add(
    //            new AdapterRegistration(typeId, adapterType, adapterConfigurationType));
    //    }
    //}

    //public class AdapterRegistration
    //{
    //    internal AdapterRegistration(
    //        Guid typeId,
    //        Type adapterType,
    //        Type adapterConfigurationType)
    //    {
    //        this.TypeId = typeId;
    //        this.AdapterType = adapterType;
    //        this.AdapterConfigurationType = adapterConfigurationType;
    //    }

    //    public Guid TypeId { get; }

    //    public Type AdapterType { get; }

    //    public Type AdapterConfigurationType { get; }
    //}
}
