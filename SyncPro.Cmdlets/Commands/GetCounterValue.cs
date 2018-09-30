namespace SyncPro.Cmdlets.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Data.Entity;
    using System.Linq;
    using System.Management.Automation;

    using SyncPro.Data;

    [Cmdlet(VerbsCommon.Get, "SyncProCounterValue")]
    public class GetCounterValue : PSCmdlet
    {
        [Parameter]
        public int CounterId { get; set; }

        protected override void ProcessRecord()
        {
            DateTime epoch = new DateTime(1970, 1, 1);

            using (var db = new CounterDatabase())
            {
                List<CounterInstanceData> ins = db.Instances
                    .Where(i => i.Id == this.CounterId)
                    .Include(i => i.Dimensions)
                    .ToList();

                if (!ins.Any())
                {
                    throw new Exception("No counters found with ID " + this.CounterId);
                }

                CounterInstanceData instance = ins.First();

                foreach (var val in db.Values.Where(v => v.CounterInstanceId == instance.Id))
                {
                    SyncProCounterValue temp = new SyncProCounterValue
                    {
                        Count = val.Count,
                        Timestamp = epoch.AddSeconds(val.Timestamp),
                        Value = val.Value,
                    };

                    this.WriteObject(temp);
                }
            }
        }
    }
}