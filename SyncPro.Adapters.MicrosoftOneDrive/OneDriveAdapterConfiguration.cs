namespace SyncPro.Adapters.MicrosoftOneDrive
{
    using System;

    using SyncPro.Configuration;
    using SyncPro.OAuth;

    public class OneDriveAdapterConfiguration : AdapterConfiguration
    {
        public override Guid AdapterTypeId => OneDriveAdapter.TargetTypeId;

        public TokenResponse CurrentToken { get; set; }

        public string CurrentWindowsLiveId { get; set; }

        public string UserId { get; set; }

        public string TargetPath { get; set; }

        public string LatestDeltaToken { get; set; }
    }
}