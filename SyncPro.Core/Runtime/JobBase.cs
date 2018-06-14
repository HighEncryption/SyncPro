namespace SyncPro.Runtime
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using SyncPro.Tracing;

    /// <summary>
    /// Enumeration of the options for the result of a job
    /// </summary>
    public enum JobResult
    {
        Undefined,
        Success,
        Warning,
        Error,
        NotRun,
        Cancelled,
    }

    public abstract class JobBase
    {
        public SyncRelationship Relationship { get; }

        /// <summary>
        /// The total number of files processed by the job
        /// </summary>
        public int FilesTotal { get; protected set; }

        /// <summary>
        /// The total number of bytes processed by the job
        /// </summary>
        public long BytesTotal { get; protected set; }

        /// <summary>
        /// The datetime when the job was started
        /// </summary>
        public DateTime StartTime { get; private set; }

        /// <summary>
        /// The datetime when the job finished
        /// </summary>
        public DateTime? EndTime { get; protected set; }

        /// <summary>
        /// The result of the sync job
        /// </summary>
        public JobResult JobResult { get; protected set; }

        /// <summary>
        /// Indicates whether the job has started
        /// </summary>
        public bool HasStarted => this.StartTime != DateTime.MinValue;

        /// <summary>
        /// Indicates whether the job has finished
        /// </summary>
        public bool HasFinished => this.EndTime != null;

        public JobBase ContinuationJob { get; set; }

        private CancellationTokenSource cancellationTokenSource;

        protected CancellationToken CancellationToken => this.cancellationTokenSource.Token;

        protected JobBase(SyncRelationship relationship)
        {
            Pre.ThrowIfArgumentNull(relationship, "relationship");

            this.Relationship = relationship;
        }

        protected JobBase(SyncRelationship relationship, DateTime startTime, DateTime? endTime)
            : this(relationship)
        {
            this.StartTime = startTime;

            if (endTime != null)
            {
                this.EndTime = endTime;
            }
        }

        public Task Start()
        {
            this.cancellationTokenSource = new CancellationTokenSource();

            this.StartTime = DateTime.Now;
            this.Relationship.State = SyncRelationshipState.Running;
            this.Relationship.ActiveJob = this;

            this.Started?.Invoke(this, new JobStartedEventArgs(this));

            Logger.JobStart(
                this.GetType().Name,
                this.Relationship.Configuration.RelationshipId);

            Task task = Task.Run(this.ExecuteTask, this.cancellationTokenSource.Token)
                .ContinueWith(this.ExecuteTaskComplete);

            task.ConfigureAwait(false);

            return task;
        }

        private void ExecuteTaskComplete(Task obj)
        {
            if (this.EndTime == null)
            {
                this.EndTime = DateTime.Now;
            }

            Logger.JobStop(
                this.GetType().Name,
                this.Relationship.Configuration.RelationshipId);

            this.Relationship.State = SyncRelationshipState.Idle;
            this.Relationship.ActiveJob = null;

            this.Finished?.Invoke(this, new JobFinishedEventArgs(this));

            this.ContinuationJob?.Start();
        }

        protected abstract Task ExecuteTask();

        public void Cancel()
        {
            this.cancellationTokenSource.Cancel();
        }

        public event EventHandler<JobStartedEventArgs> Started;

        public event EventHandler<JobFinishedEventArgs> Finished;
    }
}