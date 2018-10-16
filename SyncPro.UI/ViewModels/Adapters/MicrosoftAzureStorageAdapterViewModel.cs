namespace SyncPro.UI.ViewModels.Adapters
{
    using System;

    using SyncPro.Adapters;
    using SyncPro.Adapters.MicrosoftAzureStorage;

    public class MicrosoftAzureStorageAdapterViewModel : SyncAdapterViewModel
    {
        public static readonly Guid TargetTypeId = AzureStorageAdapter.TargetTypeId;
        
        public override string DisplayName => "Microsoft Azure Storage";

        public override string ShortDisplayName => "Azure Storage";

        public override string LogoImage => "/SyncPro.UI;component/Resources/ProviderLogos/windows_azure.png";

        public AzureStorageAdapter Adapter => (AzureStorageAdapter) this.AdapterBase;

        public override string DestinationPath { get; set; }

        public string ContainerName { get; set; }

        public string AccountName { get; set; }

        

        public MicrosoftAzureStorageAdapterViewModel(AdapterBase adapter) : base(adapter)
        {
        }

        public override void LoadContext()
        {
        }

        public override void SaveContext()
        {
        }

        public override Type GetAdapterType()
        {
            return typeof(AzureStorageAdapter);
        }

        public static MicrosoftAzureStorageAdapterViewModel CreateFromRelationship(SyncRelationshipViewModel relationship, bool isSourceAdapter)
        {
            ISyncTargetViewModel existingAdapter =
                isSourceAdapter ? relationship.SyncSourceAdapter : relationship.SyncDestinationAdapter;
            MicrosoftAzureStorageAdapterViewModel model = existingAdapter as MicrosoftAzureStorageAdapterViewModel;
            if (model != null)
            {
                return model;
            }

            MicrosoftAzureStorageAdapterViewModel adapterViewModel = relationship.CreateAdapterViewModel<MicrosoftAzureStorageAdapterViewModel>();

            // If we are creating a new adapter view model (and adapter), set the IsOriginator property
            adapterViewModel.Adapter.Configuration.IsOriginator = isSourceAdapter;

            return adapterViewModel;
        }
    }
}