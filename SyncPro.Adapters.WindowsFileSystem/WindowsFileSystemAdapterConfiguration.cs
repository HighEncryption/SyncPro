namespace SyncPro.Adapters.WindowsFileSystem
{
    using System;

    using SyncPro.Configuration;

    public class WindowsFileSystemAdapterConfiguration : AdapterConfiguration
    {
        public override Guid AdapterTypeId => WindowsFileSystemAdapter.TargetTypeId;

        public string RootDirectory { get; set; }
    }
}