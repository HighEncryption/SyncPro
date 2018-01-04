namespace SyncPro.Data
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    using SyncPro.Runtime;

    [Table("History")]
    public class SyncHistoryData
    {
        [Key]
        public int Id { get; set; }

        public DateTime Start { get; set; }

        public DateTime? End { get; set; }

        // EntityFramework does not support unsigned fields, so we will use int and long here 
        // and live with the minor loss in value size.
        public int TotalFiles { get; set; }

        public long TotalBytes { get; set; }

        public SyncTriggerType TriggeredBy { get; set; }

        public SyncJobResult Result { get; set; }
    }
}