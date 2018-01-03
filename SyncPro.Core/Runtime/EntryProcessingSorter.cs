namespace SyncPro.Runtime
{
    using System;
    using System.Collections.Generic;

    using SyncPro.Adapters;

    internal class EntryProcessingSorter : IComparer<EntryUpdateInfo>
    {
        public int Compare(EntryUpdateInfo x, EntryUpdateInfo y)
        {
            if (x == null && y == null)
            {
                return 0;
            }

            if (x == null)
            {
                return -1;
            }

            if (y == null)
            {
                return 1;
            }

            // Deleted items come after non-deleted items
            if (x.HasSyncEntryFlag(SyncEntryChangedFlags.Deleted) &&
                !y.HasSyncEntryFlag(SyncEntryChangedFlags.Deleted))
            {
                return 1;
            }

            // Save as above with reversed order
            if (!x.HasSyncEntryFlag(SyncEntryChangedFlags.Deleted) &&
                y.HasSyncEntryFlag(SyncEntryChangedFlags.Deleted))
            {
                return -1;
            }

            // NewDirectory items come before non-NewDirectory items
            if (x.HasSyncEntryFlag(SyncEntryChangedFlags.NewDirectory) &&
                !y.HasSyncEntryFlag(SyncEntryChangedFlags.NewDirectory))
            {
                return -1;
            }

            // Save as above with reversed order
            if (!x.HasSyncEntryFlag(SyncEntryChangedFlags.NewDirectory) &&
                y.HasSyncEntryFlag(SyncEntryChangedFlags.NewDirectory))
            {
                return 1;
            }

            // Sort the delete list in reverse alpha order. This *should* put deletes of 
            // children ahead of parents.
            if (x.HasSyncEntryFlag(SyncEntryChangedFlags.Deleted) &&
                y.HasSyncEntryFlag(SyncEntryChangedFlags.Deleted))
            {
                return StringComparer.Ordinal.Compare(y.RelativePath, x.RelativePath);
            }

            // All other cases, sort by name
            return StringComparer.Ordinal.Compare(x.RelativePath, y.RelativePath);
        }
    }
}