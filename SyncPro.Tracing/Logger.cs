namespace SyncPro.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    using JetBrains.Annotations;

    using Microsoft.Diagnostics.Tracing;

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
                string.Format("{0}\r\n\r\n{1}", formattedMessage, exception));
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

            return sb.ToString();
        }

        private static void LogMessageWithProperties(
            Action<string> adapterLoaded, 
            string message, 
            IEnumerable<KeyValuePair<string, object>> properties)
        {
            adapterLoaded(BuildEventMessageWithProperties(message, properties));
        }
    }

    [EventSource(Name = "SyncPro-Tracing")]
    public sealed class SyncProEventSource : EventSource
    {
        public static SyncProEventSource Log = new SyncProEventSource();

        #region Generic Events

         [Event(
            EventIDs.LogError,
            Channel = EventChannel.Operational,
            Level = EventLevel.Error, 
            Task = Tasks.General,
            Opcode = Opcodes.Error,
            Keywords = EventKeywords.None,
            Message = "{0}")]
        public void LogError(string message)
        {
            this.WriteEvent(EventIDs.LogError, message);
        }

        [Event(
            EventIDs.LogWarning,
            Channel = EventChannel.Operational,
            Task = Tasks.General,
            Opcode = Opcodes.Warning,
            Level = EventLevel.Warning,
            Keywords = EventKeywords.None,
            Message = "{0}")]
        public void LogWarning(string message)
        {
            this.WriteEvent(EventIDs.LogWarning, message);
        }

        [Event(
            EventIDs.LogInformational,
            Channel = EventChannel.Operational,
            Level = EventLevel.Informational,
            Opcode = Opcodes.Informational,
            Task = Tasks.General,
            Keywords = EventKeywords.None,
            Message = "{0}")]
        public void LogInformational(string message)
        {
            this.WriteEvent(EventIDs.LogInformational, message);
        }

        [Event(
            EventIDs.LogVerbose,
            Channel = EventChannel.Operational,
            Level = EventLevel.Verbose,
            Opcode = Opcodes.Verbose,
            Task = Tasks.General,
            Keywords = EventKeywords.None,
            Message = "{0}")]
        public void LogVerbose(string message)
        {
            this.WriteEvent(EventIDs.LogVerbose, message);
        }

        [Event(
            EventIDs.LogDebug,
            Channel = EventChannel.Analytic,
            Level = EventLevel.Verbose,
            Opcode = Opcodes.Debug,
            Task = Tasks.General,
            Message = "{0}")]
        public void LogDebug(string message)
        {
            this.WriteEvent(EventIDs.LogDebug, message);
        }

        #endregion

        [Event(
            EventIDs.GlobalInitializationComplete,
            Channel = EventChannel.Operational,
            Level = EventLevel.Verbose,
            Message = "Global initialization complete.\n\nAssemblyLocation={0}\nAppDataRoot={1}")]
        public void GlobalInitComplete(string assemblyLocation, string appDataRoot)
        {
            this.WriteEvent(
                EventIDs.GlobalInitializationComplete,
                assemblyLocation,
                appDataRoot);
        }

        [Event(
            EventIDs.AdapterLoaded,
            Channel = EventChannel.Operational,
            Level = EventLevel.Informational,
            Message = "{0}")]
        public void AdapterLoaded(string message)
        {
            this.WriteEvent(EventIDs.AdapterLoaded, message);
        }

        [Event(
            EventIDs.RelationshipLoaded,
            Channel = EventChannel.Operational,
            Level = EventLevel.Informational,
            Message = "{0}")]
        public void RelationshipLoaded(string message)
        {
            this.WriteEvent(EventIDs.RelationshipLoaded, message);
        }

        [Event(
            EventIDs.AnalyzeChangesStart,
            Channel = EventChannel.Operational,
            Level = EventLevel.Informational,
            Message = "{0}")]
        public void AnalyzeChangesStart(string message)
        {
            this.WriteEvent(EventIDs.AnalyzeChangesStart, message);
        }

        [Event(
            EventIDs.AnalyzeChangesEnd,
            Channel = EventChannel.Operational,
            Level = EventLevel.Informational,
            Message = "{0}")]
        public void AnalyzeChangesEnd(string message)
        {
            this.WriteEvent(EventIDs.AnalyzeChangesEnd, message);
        }

        [Event(
            EventIDs.AnalyzeChangesFoundChange,
            Channel = EventChannel.Operational,
            Level = EventLevel.Informational,
            Message = "{0}")]
        public void AnalyzeChangesFoundChange(string message)
        {
            this.WriteEvent(EventIDs.AnalyzeChangesFoundChange, message);
        }

        public class EventIDs
        {
            public const int LogCritical = 1;
            public const int LogError = 2;
            public const int LogWarning = 3;
            public const int LogInformational = 4;
            public const int LogVerbose = 5;
            public const int LogDebug = 6;
            public const int GlobalInitializationComplete = 7;
            public const int AdapterLoaded = 8;
            public const int RelationshipLoaded = 9;
            public const int AnalyzeChangesStart = 10;
            public const int AnalyzeChangesEnd = 11;
            public const int AnalyzeChangesFoundChange = 12;
        }

        public class Tasks
        {
            public const EventTask General = (EventTask)0x01;
        }

        public class Opcodes
        {
            public const EventOpcode Error = (EventOpcode) 0x0b;
            public const EventOpcode Warning = (EventOpcode) 0x0c;
            public const EventOpcode Informational = (EventOpcode) 0x0d;
            public const EventOpcode Verbose = (EventOpcode) 0x0e;
            public const EventOpcode Debug = (EventOpcode) 0x0f;
        }
    }

    public static class StringBuilderExtensions
    {
        [StringFormatMethod("format")]
        public static StringBuilder AppendLineFeed(this StringBuilder stringBuilder, string format, params object[] args)
        {
            return stringBuilder.Append(
                string.Format(format + "\n", args));
        }
    }
}
