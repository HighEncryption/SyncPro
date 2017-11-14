namespace SyncPro.UI.Framework
{
    using System;
    using System.Linq;
    using System.Reflection;

    internal static class TypeHelper
    {
        public static bool PropertyImplementsInterface(PropertyInfo propInfo, Type interfaceType)
        {
            if (propInfo == null)
            {
                return false;
            }

            return ImplementsInterface(propInfo.PropertyType, interfaceType);
        }

        public static bool ImplementsInterface(Type instanceType, Type interfaceType)
        {
            if (instanceType == null)
            {
                return false;
            }

            if (instanceType == interfaceType)
            {
                return true;
            }

            return instanceType.GetInterfaces().Any(implementedType => implementedType == interfaceType);
        }
    }
}