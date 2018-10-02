namespace SyncPro.UI.ViewModels.Adapters
{
    using System;

    public class PlaceholderAdapterViewModel : SyncAdapterViewModel
    {
        public PlaceholderAdapterViewModel() 
            : base(null)
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
            return null;
        }

        public override string DisplayName => "Select a provider...";

        public override string ShortDisplayName => string.Empty;

        public override string DestinationPath { get; set; }
    }
}