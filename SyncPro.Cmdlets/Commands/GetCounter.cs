namespace SyncPro.Cmdlets.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Data.Entity;
    using System.Linq;
    using System.Management.Automation;

    using SyncPro.Data;

    public class SyncProCounterValue
    {
        public string Name { get; set; }

        public string Dimensions { get; set; }

        public DateTime Timestamp { get; set; }

        public long Value { get; set; }

        public long Count { get; set; }
    }

    [Cmdlet(VerbsCommon.Get, "SyncProCounter")]
    public class GetCounter : PSCmdlet
    {
        protected override void ProcessRecord()
        {
            using (var db = new CounterDatabase())
            {
                DateTime epoch = new DateTime(1907, 1, 1);
                long ts = Convert.ToInt64(DateTime.UtcNow.Subtract(epoch).TotalSeconds);

                List<CounterValueData> values = db.Values.ToList(); //.Where(v => v.Timestamp > ts).ToList();
                List<long> counterInstanceIds = values.Select(v => v.CounterInstanceId).ToList();
                var foo = new HashSet<long>(counterInstanceIds);
                foreach (long l in foo)
                {
                    List<CounterInstanceData> ins = db.Instances.Where(i => i.Id == l).Include(i => i.Dimensions).ToList();
                    CounterInstanceData instance = ins.First();

                    foreach (var val in values.Where(v => v.CounterInstanceId == instance.Id))
                    {
                        SyncProCounterValue temp = new SyncProCounterValue()
                        {
                            Name = instance.Name,
                            Count = val.Count,
                            Timestamp = epoch.AddSeconds(val.Timestamp),
                            Value = val.Value
                        };

                        temp.Dimensions = string.Join(
                            "",
                            instance.Dimensions.Select(d => string.Format("[{0}={1}]", d.Name, d.Value)));

                        this.WriteObject(temp);
                    }
                }
            }
        }
    }
}