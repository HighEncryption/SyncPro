namespace SyncPro.UI.ViewModels.Errors
{
    using SyncPro.UI.Framework;
    using SyncPro.UI.ViewModels.Adapters;

    public class OneDriveTokenExpiredErrorViewModel : ViewModelBase
    {
        public OneDriveAdapterViewModel AdapterViewModel { get; set; }

        public OneDriveTokenExpiredErrorViewModel(OneDriveAdapterViewModel adapterViewModel)
        {
            this.AdapterViewModel = adapterViewModel;
        }
    }

    public class GenericAdapterErrorViewModel : ViewModelBase
    {
        public string ErrorText { get; set; }
    }
}