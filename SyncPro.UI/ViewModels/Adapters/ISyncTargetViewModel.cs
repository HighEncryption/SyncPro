namespace SyncPro.UI.ViewModels.Adapters
{
    using System;

    using SyncPro.Adapters;

    // TODO: Rename to ISyncAdapterViewModel
    public interface ISyncTargetViewModel
    {
        void LoadContext();

        void SaveContext();

        string LogoImage { get; }

        Type GetAdapterType();

        AdapterBase AdapterBase { get; }

        string DisplayName { get; }

        string ShortDisplayName { get; }

        string DestinationPath { get; set; }
    }
}