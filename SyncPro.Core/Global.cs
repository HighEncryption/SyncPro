namespace SyncPro
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;

    using Newtonsoft.Json;

    using SyncPro.Configuration;
    using SyncPro.Runtime;
    using SyncPro.Tracing;

    public static class Global
    {
        public static bool IsInitialized { get; private set; }

        public static void Initialize(bool testMode)
        {
            string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            Initialize(Path.Combine(localAppDataPath, "SyncPro"), testMode);

            Global.IsInitialized = true;
        }

        // Test Hook
        internal static void Initialize(string root, bool isTestMode = false)
        {
            Global.AppDataRoot = root;

            if (!Directory.Exists(Global.AppDataRoot))
            {
                Directory.CreateDirectory(Global.AppDataRoot);
            }

            Global.AppConfigFilePath = Path.Combine(
                root,
                ApplicationConfiguration.DefaultFileName);

            if (File.Exists(Global.AppConfigFilePath))
            {
                Global.AppConfig = JsonConvert.DeserializeObject<ApplicationConfiguration>(
                    File.ReadAllText(Global.AppConfigFilePath));
            }
            else
            {
                Global.AppConfig = new ApplicationConfiguration();
            }

            Logger.GlobalInitComplete(
                Assembly.GetExecutingAssembly().Location,
                Global.AppDataRoot);
        }

        static Global()
        {
            Global.SyncRelationships = new List<SyncRelationship>();
        }

        public static string AppDataRoot { get; private set; }

        public static string AppConfigFilePath { get; private set; }

        public static ApplicationConfiguration AppConfig { get; private set; }

        public static List<SyncRelationship> SyncRelationships { get; }

        public static SyncRelationship SelectedSyncRelationship { get; set; }
    }
}