namespace SyncPro.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    using Newtonsoft.Json;

    public class RelationshipConfiguration
    {
        public const string DefaultFileName = "configuration.json";

        public RelationshipConfiguration()
        {
            this.Adapters = new List<AdapterConfiguration>();
            this.TriggerConfiguration = new TriggerConfiguration();
            this.ThrottlingConfiguration = new ThrottlingConfiguration();
        }

        // A global ID for this relationship
        public Guid RelationshipId { get; set; }

        /// <summary>
        /// The user-supplied name of the relationship
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The user-supplied description of the relationship
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The date/time when the relationship was initially created
        /// </summary>
        public DateTime InitiallyCreatedUtc { get; set; }

        /// <summary>
        /// The set of adapters that belong to this relationship
        /// </summary>
        public List<AdapterConfiguration> Adapters { get; set; }

        public int SourceAdapterId { get; set; }

        public int DestinationAdapterId { get; set; }

        /// <summary>
        /// Specifies the type of triggering used
        /// </summary>
        public TriggerConfiguration TriggerConfiguration { get; set; }

        public SyncScopeType Scope { get; set; }

        /// <summary>
        /// Whether the attributes (system, hidden, etc.) of the item should be synchronzied.
        /// </summary>
        public bool SyncAttributes { get; set; }

        public ThrottlingConfiguration ThrottlingConfiguration { get; set; }

        public static RelationshipConfiguration Load(string path, string configFileName = DefaultFileName)
        {
            string configJson = File.ReadAllText(Path.Combine(path, configFileName));

            return JsonConvert.DeserializeObject<RelationshipConfiguration>(configJson);
        }

        public void Save(string path, string configFileName = DefaultFileName)
        {
            string configJson = JsonConvert.SerializeObject(this, Formatting.Indented);

            File.WriteAllText(
                Path.Combine(path, configFileName),
                configJson);
        }
    }
}
