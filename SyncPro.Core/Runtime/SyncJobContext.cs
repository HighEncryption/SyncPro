namespace SyncPro.Runtime
{
    using System;
    using System.Threading;

    public class SyncJobContext
    {
        public Guid RelationshipGuid { get; }

        public int JobId { get; }

        public SyncJobContext(SyncJob job)
        {
            this.JobId = job.Id;
            this.RelationshipGuid = job.Relationship.Configuration.RelationshipId;
        }

        private static AsyncLocal<SyncJobContext> currentContext;

        public static SyncJobContext Current
        {
            get => currentContext?.Value;
            set
            {
                if (currentContext == null)
                {
                    currentContext = new AsyncLocal<SyncJobContext>();
                }

                currentContext.Value = value;
            }
        }

        public static void Reset()
        {
            currentContext = null;
        }
    }
}