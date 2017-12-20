namespace SyncPro.UI.ViewModels.Adapters
{
    using System;

    using SyncPro.Adapters;
    using SyncPro.Adapters.BackblazeB2;
    using SyncPro.Adapters.GoogleDrive;
    using SyncPro.Adapters.MicrosoftOneDrive;
    using SyncPro.Adapters.WindowsFileSystem;
    using SyncPro.Runtime;
    using SyncPro.UI.Framework;

    public abstract class SyncAdapterViewModel : ViewModelBase<AdapterBase>, ISyncTargetViewModel
    {
        public AdapterBase AdapterBase => this.BaseModel;

        public abstract string DisplayName { get; }

        public abstract string ShortDisplayName { get; }

        public abstract string DestinationPath { get; set; }

        public virtual string LogoImage => null;

        protected SyncAdapterViewModel(AdapterBase adapter)
            : base(adapter, true)
        {
        }

        public abstract Type GetAdapterType();
    }

    // TODO: RENAME THIS TO SyncAdapterViewModelFactory
    public sealed class SyncTargetViewModelFactory
    {
        public static ISyncTargetViewModel FromAdapter(AdapterBase adapter)
        {
            if (adapter.GetTargetTypeId() == WindowsFileSystemAdapter.TargetTypeId)
            {
                return new WindowsFileSystemAdapterViewModel((WindowsFileSystemAdapter)adapter);
            }

            if (adapter.GetTargetTypeId() == OneDriveAdapter.TargetTypeId)
            {
                return new OneDriveAdapterViewModel((OneDriveAdapter)adapter);
            }

            throw new NotImplementedException();
        }

        public static ISyncTargetViewModel CreateFromViewModelType<TAdapter>(SyncRelationship relationship)
        {
            if (typeof(TAdapter) == typeof(OneDriveAdapterViewModel))
            {
                return new OneDriveAdapterViewModel(
                    new OneDriveAdapter(relationship));
            }

            if (typeof(TAdapter) == typeof(WindowsFileSystemAdapterViewModel))
            {
                return new WindowsFileSystemAdapterViewModel(
                    new WindowsFileSystemAdapter(relationship));
            }

            if (typeof(TAdapter) == typeof(GoogleDriveAdapterViewModel))
            {
                return new GoogleDriveAdapterViewModel(
                    new GoogleDriveAdapter(relationship));
            }

            if (typeof(TAdapter) == typeof(BackblazeB2AdapterViewModel))
            {
                return new BackblazeB2AdapterViewModel(
                    new BackblazeB2Adapter(relationship));
            }

            throw new NotImplementedException();
        }
    }
}