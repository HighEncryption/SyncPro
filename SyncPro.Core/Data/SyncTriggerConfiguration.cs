namespace SyncPro.Data
{
    public enum SyncScheduleInterval
    {
        Undefined,
        Hourly,
        Daily,
        Weekly,
        Monthly
    }

    public class SyncTriggerConfiguration
    {
        public SyncScheduleInterval ScheduleInterval { get; set; }

        public int HourlyIntervalValue { get; set; }

        public int HourlyMinutesPastSyncTime { get; set; }
    }
}