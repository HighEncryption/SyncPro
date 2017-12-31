namespace SyncPro.UnitTests
{
    using System;
    using System.Data.Entity;
    using System.IO;
    using System.Linq;

    using JsonLog;

    using SyncPro.Adapters.MicrosoftOneDrive;
    using SyncPro.Adapters.WindowsFileSystem;
    using SyncPro.Configuration;
    using SyncPro.Data;

    public static class TestHelper
    {
        public static string CreateDirectory(string rootPath, string relativePath)
        {
            string fullPath = Path.Combine(rootPath, relativePath);

            Directory.CreateDirectory(fullPath);

            return relativePath;
        }

        public static string CreateFile(string rootPath, string relativePath)
        {
            string fullPath = Path.Combine(rootPath, relativePath);

            using (FileStream fs = File.Open(fullPath, FileMode.CreateNew))
            {
                using (StreamWriter sw = new StreamWriter(fs))
                {
                    sw.Write("The file name is: " + fullPath);
                    sw.Write("Current time is: " + DateTime.Now.ToString("O"));
                }
            }

            return relativePath;
        }

        public static void LogConfiguration(RelationshipConfiguration relationship)
        {
            Logger.Info(">>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>");

            Logger.Info("Relationship:");
            Logger.Info("   Name: {0}", relationship.Name);
            Logger.Info("   Description: {0}", relationship.Description);
            Logger.Info("   Relationship Id: {0}", relationship.RelationshipId);
            Logger.Info("   Scope: {0}", relationship.Scope);
            Logger.Info("   Sync Attributes: {0}", relationship.SyncAttributes);

            Logger.Info("Trigger Configuration:");
            Logger.Info("   Trigger Type: {0}", relationship.TriggerConfiguration.TriggerType);
            if (relationship.TriggerConfiguration.TriggerType == SyncTriggerType.Scheduled)
            {
                Logger.Info("   Schedule Type: {0}", relationship.TriggerConfiguration.ScheduleInterval);
                if (relationship.TriggerConfiguration.ScheduleInterval == TriggerScheduleInterval.Hourly)
                {
                    Logger.Info("   Hourly Value: {0}", relationship.TriggerConfiguration.HourlyIntervalValue);
                    Logger.Info("   Minutes Past: {0}", relationship.TriggerConfiguration.HourlyMinutesPastSyncTime);
                }
            }

            Logger.Info("   Adapters: {0}", string.Join(",", relationship.Adapters.Select(a => a.Id)));

            foreach (AdapterConfiguration adapter in relationship.Adapters)
            {
                Logger.Info("Adapter {0}:", adapter.Id);
                Logger.Info("   Flags: {0}", JoinFlags<AdapterFlags>(adapter.Flags));
                Logger.Info("   Adapter Type Id: {0}", adapter.AdapterTypeId);
                Logger.Info("   Root Index Entry Id: {0}", adapter.RootIndexEntryId);

                OneDriveAdapterConfiguration oneDriveConfig = adapter as OneDriveAdapterConfiguration;
                if (oneDriveConfig != null)
                {
                    Logger.Info("   Target Path: {0}", oneDriveConfig.TargetPath);
                    Logger.Info("   Current Windows Live Id: {0}", oneDriveConfig.CurrentWindowsLiveId);
                    Logger.Info("   Latest Delta Token: {0}", oneDriveConfig.LatestDeltaToken);
                    Logger.Info("   User Id: {0}", oneDriveConfig.UserId);
                }

                WindowsFileSystemAdapterConfiguration windowsConfig = 
                    adapter as WindowsFileSystemAdapterConfiguration;
                if (windowsConfig != null)
                {
                    Logger.Info("   Root Directory: {0}", windowsConfig.RootDirectory);
                }
            }

        }

        public static void LogDatabase(SyncDatabase db)
        {
            foreach (SyncEntry entry in db.Entries.Include(e => e.AdapterEntries).ToList())
            {
                Logger.Info("Entry {0}:", entry.Id);
                Logger.Info("   Name: {0}", entry.Name);
                Logger.Info("   Full Path: \\{0}", entry.GetRelativePath(db, "\\"));
                Logger.Info("   State: {0}", JoinFlags<AdapterFlags>(entry.State));
                Logger.Info("   Parent ID: {0}", entry.ParentId == null ? "(null)" : entry.ParentId.ToString());
                Logger.Info("   Type: {0}", entry.Type);
                Logger.Info("   Creation: {0}", entry.CreationDateTimeUtc);
                Logger.Info("   Modified: {0}", entry.ModifiedDateTimeUtc);
                Logger.Info("   Last Updated: {0}", entry.EntryLastUpdatedDateTimeUtc);
                Logger.Info("   Source Size: {0}", entry.SourceSize);
                Logger.Info("   Dest Size: {0}", entry.DestinationSize);
                Logger.Info("   Source SHA1 Hash: {0}", HashToHex(entry.SourceSha1Hash));
                Logger.Info("   Dest SHA1 Hash: {0}", HashToHex(entry.DestinationSha1Hash));
                Logger.Info("   Source MD5 Hash: {0}", HashToHex(entry.SourceMd5Hash));
                Logger.Info("   Dest MD5 Hash: {0}", HashToHex(entry.DestinationMd5Hash));
                Logger.Info("   Adapter IDs:");

                foreach (SyncEntryAdapterData adapterEntry in entry.AdapterEntries)
                {
                    Logger.Info("      AdapterEntry {0}: Adapter {1} => {2}:", adapterEntry.Id, adapterEntry.AdapterId,
                        adapterEntry.AdapterEntryId);
                }
            }

            foreach (SyncHistoryData history in db.History.ToList())
            {
                Logger.Info("History {0}:", history.Id);
                Logger.Info("   Start: {0}", history.Start);
                Logger.Info("   End: {0}", history.End);
                Logger.Info("   Result: {0}", history.Result);
                Logger.Info("   Total Files: {0}", history.TotalFiles);
                Logger.Info("   Total Bytes: {0}", history.TotalBytes);
            }

            Logger.Info("<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<");
        }

        public static string JoinFlags<TEnum>(object value)
        {
            var set = StringExtensions.GetSetFlagNames<TEnum>(value).ToList();
            if (!set.Any())
            {
                set.Add("(none)");
            }
            return string.Join(",", set);
        }

        public static string HashToHex(byte[] b)
        {
            if (b == null)
            {
                return "(null)";
            }

            if (b.Length == 0)
            {
                return "(empty)";
            }

            return "0x" + BitConverter.ToString(b).Replace("-", "");
        }
    }
}