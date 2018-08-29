namespace SyncPro.Data
{
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    [Table("CounterInstances")]
    public class CounterInstanceData
    {
        [Key]
        public long Id { get; set; }

        public string Name { get; set; }

        public int InstanceHashCode { get; set; }

        public virtual ICollection<CounterDimensionData> Dimensions { get; set; }
    }
}