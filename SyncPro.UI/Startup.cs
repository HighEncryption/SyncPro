namespace SyncPro.UI
{
    using System;
    using System.Collections.Generic;

    using JsonLog;

    using SyncPro.Utility;

    public class Startup
    {
        [STAThread]
        internal static void Main(string[] arguments)
        {
            Dictionary<string, string> args = CommandLineHelper.ParseCommandLineArgs(arguments);
            App.Start(args);
        }
    }

    public static class LoggerExtensions
    {
        public static bool LogPropertyValidation { get; set; }

        public static void LogPropertyValidationInfo(string message, params object[] args)
        {
            if (LogPropertyValidation)
            {
                Logger.Debug(message, args);
            }
        }
    }
}