namespace SyncPro.Runtime
{
    using System;
    using System.Threading.Tasks;

    public class RestoreJob : JobBase
    {
        public RestoreJob(SyncRelationship relationship) : base(relationship)
        {
        }

        public RestoreJob(SyncRelationship relationship, DateTime startTime, DateTime? endTime) : base(relationship, startTime, endTime)
        {
        }

        protected override Task ExecuteTask()
        {
            throw new NotImplementedException();
        }
    }
}