namespace SyncPro.Configuration
{
    using System;

    /// <summary>
    /// Contains the persisted settings (configuration) for the triggering of
    /// a relationship. This determines when a a sync job for relationship 
    /// should be started automatically.
    /// </summary>
    public class TriggerConfiguration
    {
        public SyncTriggerType TriggerType { get; set; }

        public TriggerScheduleInterval ScheduleInterval { get; set; }

        public int HourlyIntervalValue { get; set; }

        public int HourlyMinutesPastSyncTime { get; set; }

        public int DailyIntervalValue { get; set; }

        public TimeSpan DailyStartTime { get; set; }

        public int WeeklyIntervalValue { get; set; }

        public TimeSpan WeeklyStartTime { get; set; }

        public WeeklyDays WeeklyDays { get; set; }
    }

    public enum EncryptionMode
    {
        None = 0,
        Encrypt = 1,
        Decrypt = 2
    }

    public class EncryptionConfiguration
    {
        public EncryptionMode Mode { get; set; }

        public string CertificateThumbprint { get; set; }
    }
}