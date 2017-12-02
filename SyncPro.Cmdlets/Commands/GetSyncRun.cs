namespace SyncPro.Cmdlets.Commands
{
    using System;
    using System.Management.Automation;

    using SyncPro.Runtime;

    [Cmdlet(VerbsCommon.Get, "SyncRun")]
    public class GetSyncRun : PSCmdlet
    {
        [Parameter]
        [Alias("Rid")]
        public Guid RelationshipId { get; set; }

        [Parameter]
        public int SyncRunId { get; set; }

        protected override void ProcessRecord()
        {
            SyncRelationship relationship = CmdletCommon.GetSyncRelationship(this.RelationshipId);

            if (this.SyncRunId == 0 && relationship.ActiveSyncRun == null)
            {
                
            }
        }
    }
}
