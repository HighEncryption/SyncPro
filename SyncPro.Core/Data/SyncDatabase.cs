namespace SyncPro.Data
{
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Infrastructure;
    using System.Data.Entity.Migrations;
    using System.IO;
    using System.Linq;

    /// <summary>
    /// Represents the database containing the persisted information about the files being synchronized.
    /// </summary>
    public class SyncDatabase : DbContext
    {
        public DbSet<SyncEntry> Entries { get; set; }

        public DbSet<SyncEntryAdapterData> AdapterEntries { get; set; }

        public DbSet<SyncHistoryData> History { get; set; }

        public DbSet<SyncHistoryEntryData> HistoryEntries { get; set; }

        public SyncDatabase(Guid relationshipId)
            : base(GetConnectionStringForRelationship(relationshipId))
        {
            var objectContext = ((IObjectContextAdapter)this).ObjectContext;
            objectContext.ObjectMaterialized += (sender, args) =>
            {
                var entry = args.Entity as SyncEntry;
                if (entry != null)
                {
                    entry.OriginatingDatabase = this;
                }
            };
        }

        private static string GetConnectionStringForRelationship(Guid relationshipId)
        {
            string databasePath = GetDatabaseFilePath(relationshipId);

            return string.Format(
                @"Data Source=(LocalDb)\MSSQLLocalDB;AttachDbFilename={0};Initial Catalog={1};Integrated Security=True",
                databasePath,
                relationshipId.ToString("N"));
        }

        public static string GetDatabaseFilePath(Guid relationshipId)
        {
            return Path.Combine(Global.AppDataRoot, relationshipId.ToString("N"), "database.mdf");
        }

        /// <summary>
        /// Update an existing <see cref="SyncEntry"/> in the database with the properties values of the 
        /// given <see cref="SyncEntry"/>
        /// </summary>
        /// <param name="syncEntry">The <see cref="SyncEntry"/> to copy from</param>
        /// <returns>The updated <see cref="SyncEntry"/></returns>
        public SyncEntry UpdateSyncEntry(SyncEntry syncEntry)
        {
            SyncEntry entry = this.Entries.Include(e => e.AdapterEntries).First(e => e.Id == syncEntry.Id);

            entry.Name = syncEntry.Name;
            entry.CreationDateTimeUtc = syncEntry.CreationDateTimeUtc;
            entry.ModifiedDateTimeUtc = syncEntry.ModifiedDateTimeUtc;
            entry.EntryLastUpdatedDateTimeUtc = syncEntry.EntryLastUpdatedDateTimeUtc;
            entry.SourceSha1Hash = syncEntry.SourceSha1Hash;
            entry.DestinationSha1Hash = syncEntry.DestinationSha1Hash;
            entry.SourceMd5Hash = syncEntry.SourceMd5Hash;
            entry.DestinationMd5Hash = syncEntry.DestinationMd5Hash;
            entry.SourceSize = syncEntry.SourceSize;
            entry.DestinationSize = syncEntry.DestinationSize;
            entry.State = syncEntry.State;

            return entry;
        }
    }

    public class SyncDatabaseConfiguration : DbMigrationsConfiguration<SyncDatabase>
    {
        public SyncDatabaseConfiguration()
        {
            this.AutomaticMigrationsEnabled = true;
            this.AutomaticMigrationDataLossAllowed = false;
            this.ContextKey = "SyncPro.Data.SyncDatabase";
        }
    }
}