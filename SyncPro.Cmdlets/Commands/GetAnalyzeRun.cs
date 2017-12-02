namespace SyncPro.Cmdlets.Commands
{
    using System;
    using System.Management.Automation;

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

            this.WriteObject(relationship.ActiveAnalyzeRun);
        }
    }
}