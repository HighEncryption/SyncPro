namespace SyncProLogViewer.ViewModels
{
    using System;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Windows.Data;
    using System.Windows.Input;

    using Microsoft.Diagnostics.Tracing;
    using Microsoft.Diagnostics.Tracing.Parsers;
    using Microsoft.Diagnostics.Tracing.Session;

    public class MainWindowViewModel : ViewModelBase, IDisposable
    {
        private TraceEventSession listener;
        private string logFilePath;

        public ViewerConfiguration Config { get; }

        public ObservableCollection<LogEntry> Entries { get; }

        private ObservableCollection<ActivityInfo> topLevelActivities;

        public ObservableCollection<ActivityInfo> TopLevelActivities =>
            this.topLevelActivities ?? (this.topLevelActivities = new ObservableCollection<ActivityInfo>());

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private ActivityInfo selectedActivityInfo;

        public ActivityInfo SelectedActivityInfo
        {
            get => this.selectedActivityInfo;
            set => this.SetProperty(ref this.selectedActivityInfo, value);
        }

        public ICommand BackCommand { get; }

        private void DynamicOnAll(TraceEvent traceEvent)
        {
            if (traceEvent.EventName == "ManifestData")
            {
                return;
            }

            string activityPath = null;
            ActivityInfo existingActivity = null;
            if (StartStopActivityComputer.IsActivityPath(traceEvent.ActivityID))
            {
                activityPath = StartStopActivityComputer.ActivityPathString(traceEvent.ActivityID);
                string[] elements = activityPath.Split(new[] {"/"}, StringSplitOptions.RemoveEmptyEntries);
                ObservableCollection<ActivityInfo> list = TopLevelActivities;
                for(int i = 0; i < elements.Length; i++)
                {
                    int id = int.Parse(elements[i]);
                    existingActivity = list.FirstOrDefault(a => a.Id == id);
                    if (existingActivity == null)
                    {
                        object objActName = traceEvent.PayloadByName("activityName");
                        StringBuilder sb = new StringBuilder();
                        sb.Append("/");
                        for (int j = 0; j <= i; j++)
                        {
                            sb.Append("/");
                            sb.Append(elements[j]);
                        }
                        existingActivity = new ActivityInfo(id, sb.ToString(), Convert.ToString(objActName));
                        list.Add(existingActivity);
                    }

                    list = existingActivity.Children;
                }
            }

            var logEntry = new LogEntry()
            {
                Message = traceEvent.FormattedMessage,
                ActivityPath = activityPath,
                Timestamp = traceEvent.TimeStamp,
                Level = TraceEventLevelConverter(traceEvent.Level, traceEvent.Channel),
                ThreadId = traceEvent.ThreadID
            };

            existingActivity?.Children.Add(new ActivityInfo(logEntry));

            string line = LogEntry.Serialize(logEntry);
            File.AppendAllLines(this.logFilePath, new[] { line });

            App.DispatcherInvoke(() =>
            {
                this.Entries.Add(logEntry);
                CollectionViewSource.GetDefaultView(this.Entries).MoveCurrentToLast();
            });
        }

        private static string TraceEventLevelConverter(TraceEventLevel level, TraceEventChannel traceEventChannel)
        {
            if (traceEventChannel == (TraceEventChannel) EventChannel.Debug
                && level == TraceEventLevel.Verbose)
            {
                return "DEBUG";
            }

            switch (level)
            {
                case TraceEventLevel.Always:
                    return "ALL";
                case TraceEventLevel.Critical:
                    return "CRIT";
                case TraceEventLevel.Error:
                    return "ERROR";
                case TraceEventLevel.Warning:
                    return "WARN";
                case TraceEventLevel.Informational:
                    return "INFO";
                case TraceEventLevel.Verbose:
                    return "VERB";
                default:
                    return "????";
            }
        }

        public MainWindowViewModel()
        {
            this.Entries = new ObservableCollection<LogEntry>();

            // Load settings
            this.Config = ViewerConfiguration.LoadOrCreate(
                App.Current.ConfigDirectoryPath);

            this.BackCommand = new DelegatedCommand(
                o => this.SelectedActivityInfo = null, 
                o => this.SelectedActivityInfo != null);

            // Initialize the session listener to receive the log messages
            this.listener = new TraceEventSession("MyViewerSession");
            this.listener.Source.Dynamic.All += this.DynamicOnAll;

            listener.EnableProvider(
                TplEtwProviderTraceEventParser.ProviderGuid,
                providerLevel: TraceEventLevel.Informational,
                matchAnyKeywords: (ulong)TplEtwProviderTraceEventParser.Keywords.TasksFlowActivityIds);

            Guid eventSourceGuid = TraceEventProviders.GetEventSourceGuidFromName(
                "SyncPro-Tracing"); // Get the unique ID for the eventSouce. 
            this.listener.EnableProvider(eventSourceGuid);

            var appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SyncPro",
                "logs");

            if (!Directory.Exists(appDataDir))
            {
                Directory.CreateDirectory(appDataDir);
            }

            this.logFilePath = Path.Combine(
                appDataDir, 
                string.Format("SyncPro-{0:yyyyMMdd-HHmmss}.log", DateTime.Now));

            Task.Factory.StartNew(() => { this.listener.Source.Process(); });
        }

        public void Dispose()
        {
            if (this.listener != null)
            {
                this.listener.Dispose();
                this.listener = null;

                this.Config.Save(App.Current.ConfigDirectoryPath);
            }
        }
    }
}