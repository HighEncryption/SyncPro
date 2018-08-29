namespace SyncPro.Data
{
    using System;
    using System.Data.Entity;
    using System.IO;

    public class CounterDatabase : DbContext
    {
        private static readonly Guid InitialCatalog =
            Guid.Parse("ad9795ff-62ba-4cf0-ae51-7c206e6a5687");

        public DbSet<CounterInstanceData> Instances { get; set; }

        public DbSet<CounterDimensionData> Dimensions { get; set; }

        public DbSet<CounterValueData> Values { get; set; }


        public CounterDatabase()
            : base(GetConnectionString())
        {
        }

        private static string GetConnectionString()
        {
            string databasePath = GetDatabaseFilePath();

            return string.Format(
                @"Data Source=(LocalDb)\MSSQLLocalDB;AttachDbFilename={0};Initial Catalog={1};Integrated Security=True",
                databasePath,
                InitialCatalog.ToString("N"));
        }

        public static string GetDatabaseFilePath()
        {
            return Path.Combine(Global.AppDataRoot, "counters", "database.mdf");
        }
    }
}