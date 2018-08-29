namespace SyncPro.Data
{
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    [Table("CounterDimensions")]
    public class CounterDimensionData
    {
        [Key]
        public int Id { get; set; }

        public long CounterInstanceId { get; set; }

        [ForeignKey("CounterInstanceId")]
        public virtual CounterInstanceData CounterInstance { get; set; }

        public string Name { get; set; }

        public string Value { get; set; }

        public CounterDimensionData()
        {
        }

        public CounterDimensionData(string name, string value)
        {
            this.Name = name;
            this.Value = value;
        }
    }
}