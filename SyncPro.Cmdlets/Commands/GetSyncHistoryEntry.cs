namespace SyncPro.Cmdlets.Commands
{
    using System;
    using System.Linq;
    using System.Management.Automation;

    using SyncPro.Data;
    using SyncPro.Runtime;

    [Cmdlet(VerbsCommon.Get, "SyncProHistoryEntry")]
    public class GetSyncHistoryEntry : PSCmdlet
    {
        [Parameter]
        [Alias("Rid")]
        public Guid RelationshipId { get; set; }

        [Parameter]
        public int SyncHistoryId { get; set; }

        [Parameter(
            Position = 0,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        public SyncHistoryData SyncHistory { get; set; }

        protected override void ProcessRecord()
        {
            SyncRelationship relationship = CmdletCommon.GetSyncRelationship(this.RelationshipId);

            using (var db = relationship.GetDatabase())
            {
                if (this.SyncHistory != null && this.SyncHistoryId == 0)
                {
                    this.SyncHistoryId = this.SyncHistory.Id;
                }

                var entries = db.HistoryEntries.Where(e => e.SyncHistoryId == this.SyncHistoryId).ToList();
                foreach (SyncHistoryEntryData entry in entries)
                {
                    this.WriteObject(new PSSyncHistoryEntry(entry));
                }
            }
        }
    }
}