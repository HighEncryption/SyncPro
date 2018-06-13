namespace SyncProLogViewer.ViewModels
{
    using System;
    using System.Collections.ObjectModel;
    using System.Diagnostics;

    [DebuggerDisplay("{ActivityPath} ({Children.Count})")]
    public class ActivityInfo : ViewModelBase
    {
        public int Id { get; }

        public string ActivityPath { get; }

        private ObservableCollection<ActivityInfo> children;

        public ObservableCollection<ActivityInfo> Children =>
            this.children ?? (this.children = new ObservableCollection<ActivityInfo>());

        private readonly LogEntry logEntry;

        public ActivityInfo(int id, string path, string name)
        {
            this.Id = id;
            this.ActivityPath = path;
            this.message = "Activity: " + name;
        }

        public ActivityInfo(LogEntry logEntry)
        {
            this.logEntry = logEntry;
            this.ActivityPath = logEntry.ActivityPath;
        }

        private readonly string message;

        public DateTime? Timestamp => this.logEntry?.Timestamp;

        public string Level => this.logEntry?.Level;

        public int? ThreadId => this.logEntry?.ThreadId;

        public string Message
        {
            get
            {
                if (this.message != null)
                {
                    return this.message;
                }

                return this.logEntry?.Message;
            }
        }
    }
}