namespace SyncProLogViewer.ViewModels
{
    using System;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Threading.Tasks;
    using System.Windows.Data;

    using Microsoft.Diagnostics.Tracing;
    using Microsoft.Diagnostics.Tracing.Session;

    public class MainWindowViewModel : ViewModelBase, IDisposable
    {
        private TraceEventSession listener;
        private string logFilePath;

        public ViewerConfiguration Config { get; }

        public ObservableCollection<LogEntry> Entries { get; }

        private void DynamicOnAll(TraceEvent traceEvent)
        {
            if (traceEvent.EventName == "ManifestData")
            {
                return;
            }

            var logEntry = new LogEntry()
            {
                Message = traceEvent.FormattedMessage,
                Timestamp = traceEvent.TimeStamp,
                Level = TraceEventLevelConverter(traceEvent.Level, traceEvent.Channel),
                ThreadId = traceEvent.ThreadID
            };

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

            // Initialize the session listener to receive the log messages
            this.listener = new TraceEventSession("MyViewerSession");
            this.listener.Source.Dynamic.All += this.DynamicOnAll;

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