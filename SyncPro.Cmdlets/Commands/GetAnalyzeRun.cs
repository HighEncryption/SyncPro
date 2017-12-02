namespace SyncPro.Cmdlets.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Management.Automation;

    using SyncPro.Adapters;
    using SyncPro.Runtime;

    [Cmdlet(VerbsCommon.Get, "AnalyzeRun")]
    public class GetAnalyzeRun : PSCmdlet
    {
        [Parameter]
        [Alias("Rid")]
        public Guid RelationshipId { get; set; }

        protected override void ProcessRecord()
        {
            SyncRelationship relationship = CmdletCommon.GetSyncRelationship(this.RelationshipId);

            if (relationship.ActiveAnalyzeRun == null)
            {
                throw new ItemNotFoundException("There is no active analyze run for this relationship");
            }

            var psRun = new PSAnalyzeRun(relationship.ActiveAnalyzeRun);

            this.WriteObject(psRun);
        }
    }

    public class PSAnalyzeRun
    {
        private readonly SyncRun syncRun;

        public DateTime StartTime => this.syncRun.StartTime;
        public DateTime? EndTime => this.syncRun.EndTime;

        public PSAnalyzeRelationshipResult AnalyzeResult { get; }

        public PSAnalyzeRun(SyncRun syncRun)
        {
            this.syncRun = syncRun;

            this.AnalyzeResult = new PSAnalyzeRelationshipResult(
                this.syncRun.AnalyzeResult);
        }
    }

    public class PSAnalyzeRelationshipResult
    {
        private readonly AnalyzeRelationshipResult result;

        public IReadOnlyList<PSAnalyzeResult> AnalyzeResults { get; }

        public PSAnalyzeRelationshipResult(AnalyzeRelationshipResult result)
        {
            this.result = result;

            this.AnalyzeResults = result.AdapterResults.SelectMany(pair =>
                pair.Value.EntryResults).Select(r => new PSAnalyzeResult(r)).ToList();
        }
    }

    public class PSAnalyzeResult
    {
        public PSAnalyzeResult(EntryUpdateInfo info)
        {
            this.Flags = info.Flags;
            this.State = info.State;
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

        public SyncEntryChangedFlags Flags { get; }

        public EntryUpdateState State { get; set; }

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