namespace SyncPro.UI.RelationshipEditor
{
    using System.Diagnostics;

    using SyncPro.UI.Framework;

    public class DaysOfWeekSelection : NotifyPropertyChangedSlim
    {
        public DaysOfWeekSelection()
        {
            this.Monday = true;
            this.Tuesday = true;
            this.Wednesday = true;
            this.Thursday = true;
            this.Friday = true;
            this.Saturday = true;
            this.Sunday = true;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool monday;

        public bool Monday
        {
            get { return this.monday; }
            set { this.SetProperty("Monday", ref this.monday, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool tuesday;

        public bool Tuesday
        {
            get { return this.tuesday; }
            set { this.SetProperty("Tuesday", ref this.tuesday, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool wednesday;

        public bool Wednesday
        {
            get { return this.wednesday; }
            set { this.SetProperty("Wednesday", ref this.wednesday, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool thursday;

        public bool Thursday
        {
            get { return this.thursday; }
            set { this.SetProperty("Thursday", ref this.thursday, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool friday;

        public bool Friday
        {
            get { return this.friday; }
            set { this.SetProperty("Friday", ref this.friday, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool saturday;

        public bool Saturday
        {
            get { return this.saturday; }
            set { this.SetProperty("Saturday", ref this.saturday, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool sunday;

        public bool Sunday
        {
            get { return this.sunday; }
            set { this.SetProperty("Sunday", ref this.sunday, value); }
        }

        public void SetFromFlags(WeeklyDays weeklyDays)
        {
            this.Sunday = (weeklyDays & WeeklyDays.Sunday) != 0;
            this.Monday = (weeklyDays & WeeklyDays.Monday) != 0;
            this.Tuesday = (weeklyDays & WeeklyDays.Tuesday) != 0;
            this.Wednesday = (weeklyDays & WeeklyDays.Wednesday) != 0;
            this.Thursday = (weeklyDays & WeeklyDays.Thursday) != 0;
            this.Friday = (weeklyDays & WeeklyDays.Friday) != 0;
            this.Saturday = (weeklyDays & WeeklyDays.Saturday) != 0;
        }

        public WeeklyDays ToFlags()
        {
            WeeklyDays value = WeeklyDays.None;

            if (this.Sunday)
            {
                value |= WeeklyDays.Sunday;
            }

            if (this.Monday)
            {
                value |= WeeklyDays.Monday;
            }

            if (this.Tuesday)
            {
                value |= WeeklyDays.Tuesday;
            }

            if (this.Wednesday)
            {
                value |= WeeklyDays.Wednesday;
            }

            if (this.Thursday)
            {
                value |= WeeklyDays.Thursday;
            }

            if (this.Friday)
            {
                value |= WeeklyDays.Friday;
            }

            if (this.Saturday)
            {
                value |= WeeklyDays.Saturday;
            }

            return value;
        }
    }
}