namespace SyncPro
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;

    using SyncPro.Runtime;
    using SyncPro.Tracing;

    public static class Global
    {
        public static bool IsInitialized { get; private set; }

        public static void Initialize(bool testMode)
        {
            string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            Global.Initialize(Path.Combine(localAppDataPath, "SyncPro"), testMode);

            IsInitialized = true;
        }

        // Test Hook
        internal static void Initialize(string root, bool isTestMode = false)
        {
            Global.AppDataRoot = root;

            if (!Directory.Exists(Global.AppDataRoot))
            {
                Directory.CreateDirectory(Global.AppDataRoot);
            }

            Logger.GlobalInitComplete(
                Assembly.GetExecutingAssembly().Location,
                Global.AppDataRoot);
        }

        static Global()
        {
            SyncRelationships = new List<SyncRelationship>();
        }

        public static string AppDataRoot { get; private set; }

        public static List<SyncRelationship> SyncRelationships { get; }

        public static SyncRelationship SelectedSyncRelationship { get; set; }
    }
}