namespace SyncPro.Runtime
{
    using System.Collections.Generic;
    using System.Text;

    using SyncPro.Adapters;

    public static class FlagNameCache
    {
        private static volatile object syncLock = new object();

        private static readonly Dictionary<uint, string> NameDictionary
            = new Dictionary<uint, string>();

        public static string GetNamesForFlags(SyncEntryChangedFlags flags)
        {
            lock (syncLock)
            {
                if (NameDictionary.TryGetValue((uint) flags, out string result))
                {
                    return result;
                }

                result = BuildFlagString(flags);

                NameDictionary.Add((uint) flags, result);

                return result;
            }
        }

        private static string BuildFlagString(SyncEntryChangedFlags flags)
        {
            StringBuilder sb = new StringBuilder();

            if (flags == SyncEntryChangedFlags.None)
            {
                return "None";
            }

            if ((flags & SyncEntryChangedFlags.CreatedTimestamp) != 0)
            {
                sb.Append("CreatedTimestamp,");
            }

            if ((flags & SyncEntryChangedFlags.Md5Hash) != 0)
            {
                sb.Append("Md5Hash,");
            }

            if ((flags & SyncEntryChangedFlags.Sha1Hash) != 0)
            {
                sb.Append("Sha1Hash,");
            }

            if ((flags & SyncEntryChangedFlags.Deleted) != 0)
            {
                sb.Append("Deleted,");
            }

            if ((flags & SyncEntryChangedFlags.FileSize) != 0)
            {
                sb.Append("FileSize,");
            }

            if ((flags & SyncEntryChangedFlags.ModifiedTimestamp) != 0)
            {
                sb.Append("ModifiedTimestamp,");
            }

            if ((flags & SyncEntryChangedFlags.NewDirectory) != 0)
            {
                sb.Append("NewDirectory,");
            }

            if ((flags & SyncEntryChangedFlags.NewFile) != 0)
            {
                sb.Append("NewFile,");
            }

            if ((flags & SyncEntryChangedFlags.Renamed) != 0)
            {
                sb.Append("Renamed,");
            }

            if ((flags & SyncEntryChangedFlags.Restored) != 0)
            {
                sb.Append("Restored,");
            }

            if ((flags & SyncEntryChangedFlags.DestinationExists) != 0)
            {
                sb.Append("DestinationExists,");
            }

            if ((flags & SyncEntryChangedFlags.Moved) != 0)
            {
                sb.Append("Moved,");
            }

            if ((flags & SyncEntryChangedFlags.Exception) != 0)
            {
                sb.Append("Exception,");
            }

            return sb.ToString(0, sb.Length - 1);
        }
    }
}