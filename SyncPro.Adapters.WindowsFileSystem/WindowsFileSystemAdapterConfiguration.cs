namespace SyncPro.Adapters.WindowsFileSystem
{
    using System;

    using SyncPro.Configuration;

    public class WindowsFileSystemAdapterConfiguration : AdapterConfiguration
    {
        public override Guid AdapterTypeId => WindowsFileSystemAdapter.TargetTypeId;

        public override bool DirectoriesAreUniqueEntities => true;

        public string RootDirectory { get; set; }
    }
}