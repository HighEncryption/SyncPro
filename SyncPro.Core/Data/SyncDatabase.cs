namespace SyncPro.Data
{
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Infrastructure;
    using System.Data.Entity.Migrations;
    using System.IO;
    using System.Linq;

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

        public SyncEntry UpdateSyncEntry(SyncEntry syncEntry)
        {
            SyncEntry entry = this.Entries.Include(e => e.AdapterEntries).First(e => e.Id == syncEntry.Id);

            entry.Name = syncEntry.Name;
            entry.CreationDateTimeUtc = syncEntry.CreationDateTimeUtc;
            entry.ModifiedDateTimeUtc = syncEntry.ModifiedDateTimeUtc;
            entry.EntryLastUpdatedDateTimeUtc = syncEntry.EntryLastUpdatedDateTimeUtc;
            entry.Sha1Hash = syncEntry.Sha1Hash;
            entry.Size = syncEntry.Size;
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