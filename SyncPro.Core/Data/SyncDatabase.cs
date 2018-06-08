namespace SyncPro.Data
{
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Infrastructure;
    using System.Data.Entity.Migrations;
    using System.IO;

    /// <summary>
    /// Represents the database containing the persisted information about the files being synchronized.
    /// </summary>
    public class SyncDatabase : DbContext
    {
        public DbSet<SyncEntry> Entries { get; set; }

        public DbSet<SyncEntryAdapterData> AdapterEntries { get; set; }

        public DbSet<SyncHistoryData> History { get; set; }

        public DbSet<SyncHistoryEntryData> HistoryEntries { get; set; }

        // Default constructor, only used for migrations
        public SyncDatabase()
        {}

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

        // Private constructor, used for creating a context without initializing the underlying
        // database, so that the db can be checked for an update.
        private SyncDatabase(string connectionString)
            :base(connectionString)
        {
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

        public static void UpdateIfNeeded(Guid relationshipId)
        {
            string connectionString = GetConnectionStringForRelationship(relationshipId);
            bool updateRequired;
            using (SyncDatabase db = new SyncDatabase(connectionString))
            {
                updateRequired =!db.Database.Exists() || !db.Database.CompatibleWithModel(false);
            }

            if (updateRequired)
            {
                // Create the database configuration with connection string
                SyncDatabaseConfiguration configuration = new SyncDatabaseConfiguration(
                    connectionString);

                // Create the migrator and update the database. This will also save the update.
                var migrator = new DbMigrator(configuration);
                migrator.Update();
            }
        }
    }

    public class SyncDatabaseConfiguration : DbMigrationsConfiguration<SyncDatabase>
    {
        public SyncDatabaseConfiguration(string connectionString)
        {
            this.AutomaticMigrationsEnabled = true;
            this.AutomaticMigrationDataLossAllowed = false;
            this.ContextKey = "SyncPro.Data.SyncDatabase";
            this.TargetDatabase = new DbConnectionInfo(
                connectionString, 
                "System.Data.SqlClient");
        }
    }
}