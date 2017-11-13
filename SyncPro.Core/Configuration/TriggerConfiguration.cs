namespace SyncPro.Configuration
{
    public class TriggerConfiguration
    {
        public SyncTriggerType TriggerType { get; set; }

        public TriggerScheduleInterval ScheduleInterval { get; set; }

        public int HourlyIntervalValue { get; set; }

        public int HourlyMinutesPastSyncTime { get; set; }
    }
}