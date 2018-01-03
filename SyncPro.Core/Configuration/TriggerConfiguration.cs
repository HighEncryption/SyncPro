namespace SyncPro.Configuration
{
    public class TriggerConfiguration
    {
        public SyncTriggerType TriggerType { get; set; }

        public TriggerScheduleInterval ScheduleInterval { get; set; }

        public int HourlyIntervalValue { get; set; }

        public int HourlyMinutesPastSyncTime { get; set; }
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