namespace SyncPro.Cmdlets
{
    using System.Management.Automation;

    using SyncPro.Runtime;

    [Cmdlet(VerbsCommon.Get, "SyncRelationship")]
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
