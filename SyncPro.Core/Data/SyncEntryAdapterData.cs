namespace SyncPro.Data
{
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    using SyncPro.Runtime;

    /// <summary>
    /// Contains the adapter-specific information about a <see cref="SyncEntry"/>.
    /// </summary>
    /// <remarks>
    /// The <see cref="SyncEntry"/> object contains information about a file or folder that is synchronized via
    /// a <see cref="SyncRelationship"/>. However, each adapter needs to maintain adapter-specifc information 
    /// about the <see cref="SyncEntry"/>, such as it's own internal name of the entry. This class provides the
    /// "glue" between the <see cref="SyncEntry"/> and how each adapter identifies that <see cref="SyncEntry"/>.
    /// </remarks>
    [Table("SyncEntryAdapterData")]
    public class SyncEntryAdapterData
    {
        /// <summary>
        /// The unique identifier for the <see cref="SyncEntryAdapterData"/>. This value is unique within a sync 
        /// relationship.
        /// </summary>
        [Key]
        public long Id { get; set; }

        /// <summary>
        /// The ID of the <see cref="SyncEntry"/> that this <see cref="SyncEntryAdapterData"/> is related to.
        /// </summary>
        public long SyncEntryId { get; set; }

        /// <summary>
        /// The <see cref="SyncEntry"/> that this <see cref="SyncEntryAdapterData"/> is related to.
        /// </summary>
        [ForeignKey("SyncEntryId")]
        public virtual SyncEntry SyncEntry { get; set; }

        /// <summary>
        /// The numeric identifier for this adapter.
        /// </summary>
        /// <remarks>
        /// Adapter IDs typically start at 1 and increment. Each adapter will have a unique ID within a relationship
        /// and the correspond to the ID stored in the adapter's configuration.
        /// </remarks>
        public int AdapterId { get; set; }

        /// <summary>
        /// The string that the adapter uses to uniquely idenfity this <see cref="SyncEntry"/>.
        /// </summary>
        [MaxLength(128)]
        public string AdapterEntryId { get; set; }

        /// <summary>
        /// Reserved for future use.
        /// </summary>
        public string ExtensionData { get; set; }
    }
}