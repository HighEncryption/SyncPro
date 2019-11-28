namespace SyncPro.Tracing
{
    using System;
    using System.Diagnostics.Tracing;

    [EventSource(Name = "SyncPro-Tracing")]
    public sealed class SyncProEventSource : EventSource
    {
        public static SyncProEventSource Log = new SyncProEventSource();

        #region Generic Events

        [Event(
            EventIDs.LogCritical,
            Channel = EventChannel.Debug,
            Level = EventLevel.Critical, 
            Task = Tasks.General,
            Opcode = Opcodes.Critical,
            Keywords = EventKeywords.None,
            Message = "{0}")]
        public void LogCritical(string message)
        {
            this.WriteEvent(EventIDs.LogCritical, message);
        }

        [Event(
            EventIDs.LogError,
            Channel = EventChannel.Debug,
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
            Channel = EventChannel.Debug,
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
            Channel = EventChannel.Debug,
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
            Channel = EventChannel.Debug,
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
            Channel = EventChannel.Debug,
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
            public const int AnalyzeChangesStop = 15;
            public const int AnalyzeChangesFoundChange = 16;
            public const int SynchronizeChangesStart = 17;
            public const int SynchronizeChangesEnd = 18;
            public const int ChangeSynchronized = 19;
            public const int InitializeRelationshipStart = 20;
            public const int InitializeRelationshipStop = 21;
            public const int InitializeApplicationStart = 22;
            public const int InitializeApplicationStop = 23;
            public const int InitializeAdapterStart = 24;
            public const int InitializeAdapterStop = 25;
            public const int JobStart = 26;
            public const int JobStop = 27;
        }

        public class Tasks
        {
            public const EventTask General = (EventTask)0x01;
            public const EventTask InitializeRelationship = (EventTask)0x02;
            public const EventTask InitializeApplication = (EventTask)0x03;
            public const EventTask InitializeAdapter = (EventTask)0x04;
            public const EventTask ExecuteJob = (EventTask)0x05;
            public const EventTask AnalyzeChangesWorker = (EventTask)0x06;
        }

        public class Opcodes
        {
            public const EventOpcode Critical = (EventOpcode) 0x0b;
            public const EventOpcode Error = (EventOpcode) 0x0c;
            public const EventOpcode Warning = (EventOpcode) 0x0d;
            public const EventOpcode Informational = (EventOpcode) 0x0e;
            public const EventOpcode Verbose = (EventOpcode) 0x0f;
            public const EventOpcode Debug = (EventOpcode) 0x10;
        }

        [Event(
            EventIDs.InitializeRelationshipStart,
            Channel = EventChannel.Operational,
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Start,
            Task = Tasks.InitializeRelationship,
            Message = "Initializing Relationship {0} ({1})")]
        public void InitializeRelationshipStart(string name, Guid relationshipId, string activityName)
        {
            this.WriteEvent(EventIDs.InitializeRelationshipStart, name, relationshipId, activityName);
        }

        [Event(
            EventIDs.InitializeRelationshipStop,
            Channel = EventChannel.Operational,
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Stop,
            Task = Tasks.InitializeRelationship,
            Message = "Initialize() complete for relationship {0} ({1})")]
        public void InitializeRelationshipStop(string name, Guid relationshipId)
        {
            this.WriteEvent(EventIDs.InitializeRelationshipStop, name, relationshipId);
        }

        [Event(
            EventIDs.InitializeApplicationStart,
            Channel = EventChannel.Operational,
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Start,
            Task = Tasks.InitializeApplication,
            Message = "Initializing application")]
        public void InitializeApplicationStart(string activityName)
        {
            this.WriteEvent(EventIDs.InitializeApplicationStart, activityName);
        }

        [Event(
            EventIDs.InitializeApplicationStop,
            Channel = EventChannel.Operational,
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Stop,
            Task = Tasks.InitializeApplication,
            Message = "Finished application initialization")]
        public void InitializeApplicationStop()
        {
            this.WriteEvent(EventIDs.InitializeApplicationStop);
        }

        [Event(
            EventIDs.InitializeAdapterStart,
            Channel = EventChannel.Operational,
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Start,
            Task = Tasks.InitializeAdapter,
            Message = "Initializing adapter {0} ({1}) on relationship {2}")]
        public void InitializeAdapterStart(Guid relationshipId, Guid adapterTypeId, int adapterId, string activityName)
        {
            this.WriteEvent(EventIDs.InitializeAdapterStart, relationshipId, adapterTypeId, adapterId, activityName);
        }

        [Event(
            EventIDs.InitializeAdapterStop,
            Channel = EventChannel.Operational,
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Stop,
            Task = Tasks.InitializeAdapter,
            Message = "Finished initialization of adapter {0} ({1}) on relationship {2}")]
        public void InitializeAdapterStop(Guid relationshipId, Guid adapterTypeId, int adapterId)
        {
            this.WriteEvent(EventIDs.InitializeAdapterStop, relationshipId, adapterTypeId, adapterId);
        }

        [Event(
            EventIDs.JobStart,
            Channel = EventChannel.Operational,
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Start,
            Task = Tasks.ExecuteJob,
            Message = "Started job {0} for relationship {1}")]
        public void JobStart(string name, Guid relationshipId, string activityName)
        {
            this.WriteEvent(EventIDs.JobStart, name, relationshipId, activityName);
        }

        [Event(
            EventIDs.JobStop,
            Channel = EventChannel.Operational,
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Stop,
            Task = Tasks.ExecuteJob,
            Message = "Finished job {0} for relationship {1}")]
        public void JobStop(string name, Guid relationshipId)
        {
            this.WriteEvent(EventIDs.JobStop, name, relationshipId);
        }

        [Event(
            EventIDs.AnalyzeChangesStart,
            Channel = EventChannel.Operational,
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Start,
            Task = Tasks.AnalyzeChangesWorker,
            Message = "AnalyzeJobWorker called with Id {0} for adapter {1}")]
        public void AnalyzeChangesStart(Guid runId, int adapterId, string activityName)
        {
            this.WriteEvent(EventIDs.AnalyzeChangesStart, runId, adapterId, activityName);
        }

        [Event(
            EventIDs.AnalyzeChangesStop,
            Channel = EventChannel.Operational,
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Stop,
            Task = Tasks.AnalyzeChangesWorker,
            Message = "AnalyzeJobWorker finished with Id {0} for adapter {1}")]
        public void AnalyzeChangesStop(Guid runId, int adapterId)
        {
            this.WriteEvent(EventIDs.AnalyzeChangesStop, runId, adapterId);
        }
    }
}