namespace SyncPro.Configuration
{
    using System;

    using Newtonsoft.Json;

    using SyncPro.Data;

    [JsonConverter(typeof(AdapterConfigurationConverter))]
    public abstract class AdapterConfiguration
    {
        public int Id { get; set; }

        /// <summary>
        /// A GUID that identified the type of the adapter. Set by derives classes.
        /// </summary>
        public abstract Guid AdapterTypeId { get; }

        /// <summary>
        /// The ID of the root entry in the index for this adapter. This property is only valid for originating adapters.
        /// </summary>
        public long? RootIndexEntryId { get; set; }

        //public virtual SyncEntry RootIndexEntry { get; set; }

        public AdapterFlags Flags { get; set; }

        /// <summary>
        ///  Determines if the adapter configuration has been created (saved)
        /// </summary>
        public bool IsCreated { get; set; }

        [JsonIgnore]
        public bool IsOriginator
        {
            get
            {
                return this.Flags.HasFlag(AdapterFlags.Originator);
            }

            set
            {
                if (value)
                {
                    this.Flags |= AdapterFlags.Originator;
                }
                else
                {
                    this.Flags &= ~AdapterFlags.Originator;
                }
            }
        }
    }
}