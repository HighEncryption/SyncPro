namespace SyncPro.Configuration
{
    public enum DataUsageRange
    {
        Undefined,
        Day,
        Week,
        Month
    }

    public class ThrottlingConfiguration
    {
        public bool IsEnabled { get; set; }

        public int Value { get; set; }

        public int ScaleFactor { get; set; }

        public bool IsDataUsageLimitEnabled { get; set; }

        public int DataUsageBytes{ get; set; }

        public DataUsageRange DataUsageRange { get; set; }
    }
}