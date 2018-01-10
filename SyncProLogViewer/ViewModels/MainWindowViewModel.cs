namespace SyncProLogViewer.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Threading.Tasks;
    using System.Windows.Data;

    using Microsoft.Diagnostics.Tracing;
    using Microsoft.Diagnostics.Tracing.Session;

    public class MainWindowViewModel : ViewModelBase, IDisposable
    {
        private TraceEventSession listener;

        public ObservableCollection<LogEntry> Entries { get; }

        private void DynamicOnAll(TraceEvent traceEvent)
        {
            if (traceEvent.EventName == "ManifestData")
            {
                return;
            }

            App.DispatcherInvoke(() =>
            {
                this.Entries.Add(new LogEntry()
                {
                    Message = traceEvent.FormattedMessage,
                    Timestamp = traceEvent.TimeStamp,
                    Level = TraceEventLevelConverter(traceEvent.Level),
                    ThreadId = traceEvent.ThreadID
                });

                CollectionViewSource.GetDefaultView(this.Entries).MoveCurrentToLast();
            });
        }

        private static string TraceEventLevelConverter(TraceEventLevel level)
        {
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

            string[] cmdArgs = Environment.GetCommandLineArgs();
            Dictionary<string, string> commandLineArgs = new Dictionary<string, string>();

            this.listener = new TraceEventSession("MyViewerSession");
            this.listener.Source.Dynamic.All += DynamicOnAll;

            var eventSourceGuid = TraceEventProviders.GetEventSourceGuidFromName(
                "SyncPro-Tracing"); // Get the unique ID for the eventSouce. 
            this.listener.EnableProvider(eventSourceGuid);

            Task.Factory.StartNew(() => { this.listener.Source.Process(); });
        }

        public void Dispose()
        {
            if (this.listener != null)
            {
                this.listener.Dispose();
                this.listener = null;
            }
        }
    }
}