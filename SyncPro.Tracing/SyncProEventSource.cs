namespace SyncPro.Tracing
{
    using Microsoft.Diagnostics.Tracing;

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
            EventIDs.RelationshipSaved,
            Channel = EventChannel.Operational,
            Level = EventLevel.Informational,
            Message = "{0}")]
        public void RelationshipSaved(string message)
        {
            this.WriteEvent(EventIDs.RelationshipSaved, message);
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

        [Event(
            EventIDs.SynchronizeChangesStart,
            Channel = EventChannel.Operational,
            Level = EventLevel.Informational,
            Message = "{0}")]
        public void SynchronizeChangesStart(string message)
        {
            this.WriteEvent(EventIDs.SynchronizeChangesStart, message);
        }

        [Event(
            EventIDs.SynchronizeChangesEnd,
            Channel = EventChannel.Operational,
            Level = EventLevel.Informational,
            Message = "{0}")]
        public void SynchronizeChangesEnd(string message)
        {
            this.WriteEvent(EventIDs.SynchronizeChangesEnd, message);
        }

        [Event(
            EventIDs.ChangeSynchronized,
            Channel = EventChannel.Operational,
            Level = EventLevel.Informational,
            Message = "{0}")]
        public void ChangeSynchronized(string message)
        {
            this.WriteEvent(EventIDs.ChangeSynchronized, message);
        }

        public class EventIDs
        {
            public const int LogCritical = 1;
            public const int LogError = 2;
            public const int LogWarning = 3;
            public const int LogInformational = 4;
            public const int LogVerbose = 5;
            public const int LogDebug = 6;
            public const int GlobalInitializationComplete = 10;
            public const int AdapterLoaded = 11;
            public const int RelationshipSaved = 12;
            public const int RelationshipLoaded = 13;
            public const int AnalyzeChangesStart = 14;
            public const int AnalyzeChangesEnd = 15;
            public const int AnalyzeChangesFoundChange = 16;
            public const int SynchronizeChangesStart = 17;
            public const int SynchronizeChangesEnd = 18;
            public const int ChangeSynchronized = 19;
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