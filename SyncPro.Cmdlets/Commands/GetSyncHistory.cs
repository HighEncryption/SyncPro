namespace SyncPro.Cmdlets.Commands
{
    using System;
    using System.Linq;
    using System.Management.Automation;

    using SyncPro.Adapters;
    using SyncPro.Data;
    using SyncPro.Runtime;

    [Cmdlet(VerbsCommon.Get, "SyncProHistory")]
    public class GetSyncHistory : PSCmdlet
    {
        [Parameter]
        [Alias("Rid")]
        public Guid RelationshipId { get; set; }

        [Parameter]
        public int SyncHistoryId { get; set; }

        protected override void ProcessRecord()
        {
            SyncRelationship relationship = CmdletCommon.GetSyncRelationship(this.RelationshipId);

            if (this.SyncHistoryId > 0)
            {
                this.WriteObject(
                    relationship.GetSyncRunHistory().FirstOrDefault(r => r.Id == this.SyncHistoryId));
                return;
            }

            using (var db = relationship.GetDatabase())
            {
                if (this.SyncHistoryId > 0)
                {
                    this.WriteObject(
                        db.History.FirstOrDefault(h => h.Id == this.SyncHistoryId));
                    return;
                }

                // Copy all history items to a list to prevent enumerating multiple
                // tables at the same time.
                var histories = db.History.ToList();
                foreach (var history in histories)
                {
                    this.WriteObject(history);
                }
            }
        }
    }

    public class PSSyncHistoryEntry
    {
        public PSSyncHistoryEntry(SyncHistoryEntryData info)
        {
            this.Id = info.Id;
            this.SyncEntryId = info.SyncEntryId;
            this.Flags = info.Flags;
            this.Result = info.Result;
            this.SizeOld = info.SizeOld;
            this.SizeNew = info.SizeNew;
            this.Sha1HashOld = this.GetHashString(info.Sha1HashOld);
            this.Sha1HashNew = this.GetHashString(info.Sha1HashNew);
            this.Md5HashOld = this.GetHashString(info.Md5HashOld);
            this.Md5HashNew = this.GetHashString(info.Md5HashNew);
            this.CreationDateTimeUtcOld = info.CreationDateTimeUtcOld;
            this.CreationDateTimeUtcNew = info.CreationDateTimeUtcNew;
            this.ModifiedDateTimeUtcOld = info.ModifiedDateTimeUtcOld;
            this.ModifiedDateTimeUtcNew = info.ModifiedDateTimeUtcNew;
            this.PathOld = info.PathOld;
            this.PathNew = info.PathNew;
        }

        /// <summary>
        /// The database ID of the history entry.
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// The ID of the <see cref="SyncEntry"/> this this entry refers to.
        /// </summary>
        public long SyncEntryId { get; }

        public SyncEntryChangedFlags Flags { get; }

        /// <summary>
        /// The result of the change (succeeded/failed).
        /// </summary>
        public EntryUpdateState Result { get; }

        /// <summary>
        /// The timestamp when the change was applied (and this <see cref="SyncHistoryEntryData"/> was created).
        /// </summary>
        public DateTime Timestamp { get; set; }

        public long SizeOld { get; }

        /// <summary>
        /// The size of the entry (in bytes) at the time when it was synced.
        /// </summary>
        public long SizeNew { get; }

        /// <summary>
        /// The previous SHA1 Hash of the file content (if changed)
        /// </summary>
        public string Sha1HashOld { get; }

        /// <summary>
        /// The SHA1 Hash of the file content at the time when it was synced.
        /// </summary>
        public string Sha1HashNew { get; }

        /// <summary>
        /// The previous MD5 Hash of the file content (if changed)
        /// </summary>
        public string Md5HashOld { get; }

        /// <summary>
        /// The MD5 Hash of the file content at the time when it was synced.
        /// </summary>
        public string Md5HashNew { get; }

        /// <summary>
        /// The previous CreationTime of the entry (if changed)
        /// </summary>
        public DateTime? CreationDateTimeUtcOld { get; }

        /// <summary>
        /// The CreationTime of the entry at the time it was synced.
        /// </summary>
        public DateTime? CreationDateTimeUtcNew { get; }

        /// <summary>
        /// The previous ModifiedTime of the entry (if changed)
        /// </summary>
        public DateTime? ModifiedDateTimeUtcOld { get; }

        /// <summary>
        /// The ModifiedTime of the entry at the time it was synced.
        /// </summary>
        public DateTime? ModifiedDateTimeUtcNew { get; }

        /// <summary>
        /// The previous full path of the item (from the root of the adapter) if changed.
        /// </summary>
        public string PathOld { get; }

        /// <summary>
        /// The full path of the item (from the root of the adapter) when it was synced.
        /// </summary>
        public string PathNew { get; }

        private string GetHashString(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return null;
            }

            return Convert.ToBase64String(data);
        }
    }
}