namespace SyncPro.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    using JetBrains.Annotations;

    public static class Logger
    {
        [StringFormatMethod("message")]
        public static void Error(string message, params object[] args)
        {
            SyncProEventSource.Log.LogError(string.Format(message, args));
        }

        [StringFormatMethod("message")]
        public static void Info(string message, params object[] args)
        {
            SyncProEventSource.Log.LogInformational(string.Format(message, args));
        }

        [StringFormatMethod("message")]
        public static void Warning(string message, params object[] args)
        {
            SyncProEventSource.Log.LogWarning(string.Format(message, args));
        }

        [StringFormatMethod("message")]
        public static void Verbose(string message, params object[] args)
        {
            SyncProEventSource.Log.LogVerbose(string.Format(message, args));
        }

        [StringFormatMethod("message")]
        public static void Debug(string message, params object[] args)
        {
            SyncProEventSource.Log.LogDebug(string.Format(message, args));
        }

        [StringFormatMethod("message")]
        public static void LogException(Exception exception, string message, params object[] args)
        {
            string formattedMessage = string.Format(message, args);

            SyncProEventSource.Log.LogError(
                string.Format("{0}\n\n{1}", formattedMessage, exception));
        }

        public static void GlobalInitComplete(string assemblyLocation, string appDataRoot)
        {
            SyncProEventSource.Log.GlobalInitComplete(assemblyLocation, appDataRoot);
        }

        public static void AdapterLoaded(
            string adapterTypeName, 
            Guid adapterId, 
            IEnumerable<KeyValuePair<string, object>> properties)
        {
            LogMessageWithProperties(
                SyncProEventSource.Log.AdapterLoaded,
                string.Format(
                    "Adapter {0} {1} loaded with the following properties:",
                    adapterTypeName,
                    adapterId),
                properties);
        }

        public static void RelationshipSaved(
            IEnumerable<KeyValuePair<string, object>> properties)
        {
            LogMessageWithProperties(
                SyncProEventSource.Log.RelationshipSaved,
                "The relationship was saved",
                properties);
        }

        public static void RelationshipLoaded(
            Guid relationshipId,
            IEnumerable<KeyValuePair<string, object>> properties)
        {
            LogMessageWithProperties(
                SyncProEventSource.Log.RelationshipLoaded,
                string.Format(
                    "Relationship {0} loaded with the following properties:",
                    relationshipId),
                properties);
        }

        public static void AnalyzeChangesStart(
            IEnumerable<KeyValuePair<string, object>> properties)
        {
            LogMessageWithProperties(
                SyncProEventSource.Log.AnalyzeChangesStart,
                "Beginning AnalyzeChangesFromAdapter",
                properties);
        }

        public static void AnalyzeChangesEnd(
            IEnumerable<KeyValuePair<string, object>> properties)
        {
            LogMessageWithProperties(
                SyncProEventSource.Log.AnalyzeChangesStart,
                "Finished AnalyzeChangesFromAdapter",
                properties);
        }

        public static void SyncAnalyzerChangeFound(string message)
        {
            SyncProEventSource.Log.AnalyzeChangesFoundChange(message);
        }

        public static void SynchronizeChangesStart(
            IEnumerable<KeyValuePair<string, object>> properties)
        {
            LogMessageWithProperties(
                SyncProEventSource.Log.SynchronizeChangesStart,
                "Beginning SynchronizeChanges",
                properties);
        }

        public static void SynchronizeChangesEnd(
            IEnumerable<KeyValuePair<string, object>> properties)
        {
            LogMessageWithProperties(
                SyncProEventSource.Log.SynchronizeChangesStart,
                "Finished SynchronizeChanges",
                properties);
        }

        public static void ChangeSynchronzied(string message)
        {
            SyncProEventSource.Log.ChangeSynchronized(message);
        }

        public static string BuildEventMessageWithProperties(
            string message,
            IEnumerable<KeyValuePair<string, object>> properties)
        {
            StringBuilder sb = new StringBuilder()
                .AppendLineFeed(message);

            foreach (KeyValuePair<string, object> pair in properties)
            {
                sb.AppendLineFeed("{0}: {1}", pair.Key, pair.Value);
            }

            // Return all but the last char (a \n character)
            return sb.ToString(0, sb.Length - 1);
        }

        private static void LogMessageWithProperties(
            Action<string> adapterLoaded, 
            string message, 
            IEnumerable<KeyValuePair<string, object>> properties)
        {
            adapterLoaded(BuildEventMessageWithProperties(message, properties));
        }
    }
}
