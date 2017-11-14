namespace SyncPro.UI.Utility
{
    using System;
    public class BaseModelPropertyAttribute : Attribute
    {
        public string BasePropertyName { get; set; }

        public string LocalPropertyName { get; set; }

        public bool NotifyOnPropertyChange { get; set; }
    }
}
