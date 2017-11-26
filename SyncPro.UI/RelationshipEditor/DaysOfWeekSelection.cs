namespace SyncPro.UI.RelationshipEditor
{
    using System.Diagnostics;

    using SyncPro.UI.Framework;

    public class DaysOfWeekSelection : NotifyPropertyChangedSlim
    {
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
    }
}