namespace SyncPro
{
    using System;

    public enum TriggerScheduleInterval
    {
        Undefined,
        Hourly,
        Daily,
        Weekly,
        Monthly
    }

    [Flags]
    public enum WeeklyDays
    {
        None = 0x00,
        Sunday = 0x01,
        Monday = 0x02,
        Tuesday = 0x04,
        Wednesday = 0x08,
        Thursday = 0x10,
        Friday = 0x20,
        Saturday = 0x40,
        All = WeeklyDays.Sunday | WeeklyDays.Monday | WeeklyDays.Tuesday |
              WeeklyDays.Wednesday | WeeklyDays.Thursday | WeeklyDays.Friday |
              WeeklyDays.Saturday
    }
}