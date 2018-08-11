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

            IsInitialized = true;
        }

        // Test Hook
        internal static void Initialize(string root, bool isTestMode = false)
        {
            AppDataRoot = root;

            if (!Directory.Exists(AppDataRoot))
            {
                Directory.CreateDirectory(AppDataRoot);
            }

            AppConfigFilePath = Path.Combine(
                root,
                ApplicationConfiguration.DefaultFileName);

            if (File.Exists(AppConfigFilePath))
            {
                AppConfig = JsonConvert.DeserializeObject<ApplicationConfiguration>(
                    File.ReadAllText(AppConfigFilePath));
            }
            else
            {
                AppConfig = new ApplicationConfiguration();
                SaveAppConfig();
            }

            Logger.GlobalInitComplete(
                Assembly.GetExecutingAssembly().Location,
                AppDataRoot);
        }

        public static void SaveAppConfig()
        {
            string serializedConfig = JsonConvert.SerializeObject(
                AppConfig,
                Formatting.Indented);
            File.WriteAllText(AppConfigFilePath, serializedConfig);
        }

        static Global()
        {
            SyncRelationships = new List<SyncRelationship>();
        }

        public static string AppDataRoot { get; private set; }

        public static string AppConfigFilePath { get; private set; }

        public static ApplicationConfiguration AppConfig { get; private set; }

        public static List<SyncRelationship> SyncRelationships { get; }

        public static SyncRelationship SelectedSyncRelationship { get; set; }
    }
}