namespace SyncPro.Tracing
{
    using System;

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
    }

    [EventSource(Name = "SyncPro-Tracing")]
    public sealed class SyncProEventSource : EventSource
    {
        public static SyncProEventSource Log = new SyncProEventSource();

        [Event(
            EventIDs.LogError,
            Channel = EventChannel.Operational,
            Level = EventLevel.Error, 
            Task = Tasks.General,
            Opcode = Opcodes.Error,
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

        public class EventIDs
        {
            public const int LogCritical = 1;
            public const int LogError = 2;
            public const int LogWarning = 3;
            public const int LogInformational = 4;
            public const int LogVerbose = 5;
            public const int LogDebug = 6;
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
}
