namespace SyncPro.Adapters
{
    using System;

    using SyncPro.Configuration;

    public class RestoreOnlyWindowsFileSystemAdapterConfiguration : AdapterConfiguration
    {
        public override Guid AdapterTypeId => RestoreOnlyWindowsFileSystemAdapter.TargetTypeId;

        public string RootDirectory { get; set; }
    }
}