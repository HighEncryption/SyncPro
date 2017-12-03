namespace SyncPro.Cmdlets.Commands
{
    using System.Management.Automation;

    using SyncPro.Runtime;

    [Cmdlet(VerbsCommon.Get, "SyncProRelationship")]
    public class GetSyncRelationship : PSCmdlet
    {
        protected override void ProcessRecord()
        {
            foreach (SyncRelationship syncRelationship in Global.SyncRelationships)
            {
                this.WriteObject(syncRelationship);
            }
        }
    }
}
