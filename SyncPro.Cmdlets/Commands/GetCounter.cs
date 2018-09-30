namespace SyncPro.Cmdlets.Commands
{
    using System.Data.Entity;
    using System.Linq;
    using System.Management.Automation;

    using SyncPro.Data;

    [Cmdlet(VerbsCommon.Get, "SyncProCounter")]
    public class GetCounter : PSCmdlet
    {
        protected override void ProcessRecord()
        {
            using (var db = new CounterDatabase())
            {
                var allCounters = db.Instances.Include(i => i.Dimensions).ToList();

                foreach (CounterInstanceData counterInstance in allCounters)
                {
                    SyncProCounterInstance counter = new SyncProCounterInstance
                    {
                        Id = counterInstance.Id,
                        Name = counterInstance.Name,
                        Dimensions = string.Join(
                            "",
                            counterInstance.Dimensions.Select(d => string.Format("[{0}={1}]", d.Name, d.Value)))
                    };

                    this.WriteObject(counter);
                }
            }
        }
    }
}