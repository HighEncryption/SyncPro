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