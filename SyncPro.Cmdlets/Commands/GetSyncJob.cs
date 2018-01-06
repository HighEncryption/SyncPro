namespace SyncPro.Cmdlets.Commands
{
    using System;
    using System.Management.Automation;

    using SyncPro.Runtime;

    [Cmdlet(VerbsCommon.Get, "SyncProJob")]
    public class GetSyncJob : PSCmdlet
    {
        [Parameter]
        [Alias("Rid")]
        public Guid RelationshipId { get; set; }

        [Parameter]
        public int SyncJobId { get; set; }

        protected override void ProcessRecord()
        {
            SyncRelationship relationship = CmdletCommon.GetSyncRelationship(this.RelationshipId);

            if (this.SyncJobId == 0 && relationship.ActiveJob == null)
            {
                
            }
        }
    }
}
