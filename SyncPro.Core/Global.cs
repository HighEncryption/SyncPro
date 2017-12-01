namespace SyncPro
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;

    using JsonLog;

    using SyncPro.Runtime;

    public static class Global
    {
        public static void Initialize(bool testMode)
        {
            string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            Global.Initialize(Path.Combine(localAppDataPath, "SyncPro"), testMode);
        }

        // Test Hook
        internal static void Initialize(string root, bool isTestMode = false)
        {
            Global.AppDataRoot = root;

            if (!Directory.Exists(Global.AppDataRoot))
            {
                Directory.CreateDirectory(Global.AppDataRoot);
            }

            InitializeLogging(Global.AppDataRoot, isTestMode); 

            Logger.Info("Logging initialized.");

            Logger.Info("AssemblyLocation=" + Assembly.GetExecutingAssembly().Location);
            Logger.Info("AppDataRoot=" + Global.AppDataRoot);
        }

        private static void InitializeLogging(string logDir, bool autoLaunchLogViewer)
        {
            JsonLogWriter jsonLogWriter = new JsonLogWriter();
            jsonLogWriter.Initialize(logDir);
            Logger.AddLogWriter(jsonLogWriter);

            using (File.Create(jsonLogWriter.LogFilePath))
            {
            }

            if (autoLaunchLogViewer)
            {
                PipeLogWriter pipeLogWriter = new PipeLogWriter();
                pipeLogWriter.StartInitialize();

                try
                {
                    JsonLogViewerHelper.LaunchLogViewer(
                        "/pipe " + pipeLogWriter.PipeName,
                        true);

                    pipeLogWriter.FinishInitialize();

                    Logger.AddLogWriter(pipeLogWriter);
                }
                catch (Exception e)
                {
                    pipeLogWriter.Dispose();

                    Logger.Error("Failed to start JsonLogViewer");
                    Logger.LogException(e);
                }
            }
        }

        static Global()
        {
            SyncRelationships = new List<SyncRelationship>();
        }

        public static string AppDataRoot { get; private set; }

        public static List<SyncRelationship> SyncRelationships { get; }
    }
}