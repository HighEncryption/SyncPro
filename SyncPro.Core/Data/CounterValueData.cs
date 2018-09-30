namespace SyncPro.Data
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    [Flags]
    public enum CounterValueFlags
    {
        Undefined = 0x00,
        AggregateNone = 0x01,
        Aggregate1Second = 0x02,
        Aggregate1Minute = 0x04,
    }

    [Table("CounterValues")]
    public class CounterValueData
    {
        [Key]
        public long Id { get; set; }

        public long CounterInstanceId { get; set; }

        public long Timestamp { get; set; }

        public int Flags { get; set; }

        public long Value { get; set; }

        public int Count { get; set; }
    }
}