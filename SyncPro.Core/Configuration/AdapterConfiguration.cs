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
        /// Indicates whether directories are stored as unique entities for thie type of adapter.
        /// </summary>
        /// <remarks>
        /// Some adapter types (Backblaze, Azure Storage) do not store directories as unique
        /// entities. Rather, files are the only entities stored, and each file's name is the
        /// full path of the file. For these adapter types, directories are logical and are
        /// not actually stored by the provider.
        /// </remarks>
        public abstract bool DirectoriesAreUniqueEntities { get; }

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