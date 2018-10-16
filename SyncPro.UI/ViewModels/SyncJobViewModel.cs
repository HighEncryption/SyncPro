namespace SyncPro.UI.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Data.Entity;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Input;

    using SyncPro.Adapters;
    using SyncPro.Data;
    using SyncPro.Runtime;
    using SyncPro.UI.Converters;
    using SyncPro.UI.Framework.MVVM;

    public enum SyncJobChangesDisplayMode
    {
        OnlyChanges,
        AllFiles,
    }

    public class SyncJobViewModel : JobViewModel
    {
        public SyncJob SyncJob => (SyncJob) this.Job;
        private bool loadFromHistory;

        public ICommand BeginSyncJobCommand { get; }

        public SyncJobViewModel(SyncJob syncJob, SyncRelationshipViewModel relationshipViewModel, bool loadFromHistory)
            : base(syncJob, relationshipViewModel)
        {
            this.loadFromHistory = loadFromHistory;

            this.SyncJob.Started += this.SyncJobOnSyncStarted;
            this.SyncJob.Finished += this.SyncJobOnSyncFinished;
            this.SyncJob.ProgressChanged += this.SyncJobOnProgressChanged;

            this.SyncJob.TriggerType = SyncTriggerType.Manual;

            this.BeginSyncJobCommand = new DelegatedCommand(o => this.BeginSyncJob());

            this.ChangeMetricsList = new List<ChangeMetrics>()
            {
                new ChangeMetrics("Files"),
                new ChangeMetrics("Folders"),
                new ChangeMetrics("Bytes", true)
            };

            if (syncJob.HasStarted)
            {
                this.StartTime = syncJob.StartTime;
            }

            if (syncJob.HasFinished)
            {
                this.EndTime = syncJob.EndTime ?? DateTime.MinValue;
                this.ItemsCopiedDisplayString = String.Format(
                    "{0} files / {1}",
                    this.SyncJob.FilesTotal,
                    FileSizeConverter.Convert(this.SyncJob.BytesTotal, 1));
                this.Duration = this.EndTime - this.StartTime;
                this.SetStatusDescription();
            }

            if (this.SyncRelationship.State == SyncRelationshipState.Running)
            {
                this.SyncJobOnSyncStarted(this, new EventArgs());
            }
        }

        public void BeginLoad()
        {
            if (!this.loadFromHistory)
            {
                return;
            }

            this.EntryUpdatesTreeList.Add(
                new EntryUpdateInfoViewModel()
                {
                    Name = "[root]",
                    IsDirectory = true
                });

            Task.Run(() =>
            {
                using (var db = this.SyncRelationship.GetDatabase())
                {
                    foreach (
                        SyncHistoryEntryData entry in
                            db.HistoryEntries
                                .Where(e => e.SyncHistoryId == this.SyncJob.Id)
                                .Include(e => e.SyncEntry)
                                .OrderBy(e => e.SyncEntryId))
                    {
                        this.CalculateChangeMetrics(entry);

                        this.AddEntryUpdate(
                            new EntryUpdateInfoViewModel(entry, this.SyncRelationship),
                            entry.PathNew.Split('\\').ToList());
                    }
                }

                foreach (ChangeMetrics changeMetrics in this.ChangeMetricsList)
                {
                    changeMetrics.RaisePropertiesChanged();
                }
            });

            this.loadFromHistory = false;
        }

        private void CalculateChangeMetrics(SyncHistoryEntryData entry)
        {
            if (entry.SyncEntry.Type == SyncEntryType.Directory)
            {
                if (entry.HasSyncEntryFlag(SyncEntryChangedFlags.NewDirectory))
                {
                    this.ChangeMetricsList[1].Added++;
                }
                else if (entry.HasSyncEntryFlag(SyncEntryChangedFlags.Deleted))
                {
                    this.ChangeMetricsList[1].Removed++;
                }
                else if (entry.HasSyncEntryFlag(SyncEntryChangedFlags.CreatedTimestamp)
                         || entry.HasSyncEntryFlag(SyncEntryChangedFlags.ModifiedTimestamp))
                {
                    this.ChangeMetricsList[1].Metadata++;
                }
            }
            else
            {
                if (entry.HasSyncEntryFlag(SyncEntryChangedFlags.NewFile))
                {
                    this.ChangeMetricsList[0].Added++;
                    this.ChangeMetricsList[2].Added += entry.OriginalSizeNew;
                    this.BytesToCopy += entry.OriginalSizeNew;
                }
                else if (entry.HasSyncEntryFlag(SyncEntryChangedFlags.Deleted))
                {
                    this.ChangeMetricsList[0].Removed++;
                    this.ChangeMetricsList[2].Removed += entry.OriginalSizeNew;
                }
                else if (entry.HasSyncEntryFlag(SyncEntryChangedFlags.CreatedTimestamp)
                         || entry.HasSyncEntryFlag(SyncEntryChangedFlags.ModifiedTimestamp))
                {
                    this.ChangeMetricsList[0].Metadata++;
                }
                else if (entry.HasSyncEntryFlag(SyncEntryChangedFlags.Sha1Hash)
                         || entry.HasSyncEntryFlag(SyncEntryChangedFlags.Md5Hash)
                         || entry.HasSyncEntryFlag(SyncEntryChangedFlags.FileSize))
                {
                    this.ChangeMetricsList[0].Modified++;
                    this.ChangeMetricsList[2].Modified += entry.OriginalSizeNew;
                    this.BytesToCopy += entry.OriginalSizeNew;
                }
            }
        }

        private void BeginSyncJob()
        {
            this.SyncJob.Start();
        }

        private void SetStatusDescription()
        {
            switch (this.SyncJob.JobResult)
            {
                case JobResult.Undefined:
                    this.StatusDescription = "Unknown";
                    break;
                case JobResult.Success:
                    this.StatusDescription = "Finished Successfully";
                    break;
                case JobResult.Warning:
                    this.StatusDescription = "Finished With Warnings";
                    break;
                case JobResult.Error:
                    this.StatusDescription = "Failed";
                    break;
                case JobResult.NotRun:
                    this.StatusDescription = "Sync Not Run";
                    break;
                case JobResult.Cancelled:
                    this.StatusDescription = "Cancelled";
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public JobResult JobResult => this.SyncJob.JobResult;

        private void SyncJobOnSyncFinished(object sender, EventArgs eventArgs)
        {
            this.EndTime = this.SyncJob.EndTime.Value;
            this.ItemsCopiedDisplayString = 
                string.Format(
                    "{0} files / {1}", 
                    this.SyncJob.FilesTotal, 
                    FileSizeConverter.Convert(this.SyncJob.BytesTotal, 1));

            // Calculate unchanges files and folders
            this.ChangeMetricsList[0].Unchanged = this.SyncJob.AnalyzeResult.UnchangedFileCount;
            this.ChangeMetricsList[1].Unchanged = this.SyncJob.AnalyzeResult.UnchangedFolderCount;
            this.ChangeMetricsList[2].Unchanged = this.SyncJob.AnalyzeResult.UnchangedFileBytes;

            this.SetStatusDescription();

            this.metadataUpdateCancellationToken.Cancel();
        }

        private CancellationTokenSource metadataUpdateCancellationToken;

        private void SyncJobOnSyncStarted(object sender, EventArgs eventArgs)
        {
            this.StartTime = this.SyncJob.StartTime;
            this.metadataUpdateCancellationToken = new CancellationTokenSource();

            Task t = new Task(async () =>
            {
                while (!this.metadataUpdateCancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(100);

                    foreach (ChangeMetrics changeMetric in this.ChangeMetricsList)
                    {
                        changeMetric.RaisePropertiesChanged();
                        this.RaisePropertyChanged(nameof(this.BytesToCopy));
                    }
                }
            },
            this.metadataUpdateCancellationToken.Token);

            t.Start();
        }

        private DateTime lastSyncProgressUpdate = DateTime.MinValue;

        /// <summary>
        /// During an active sync job, this methods is invoked on progress changed (aka when a change is detected).
        /// </summary>
        /// <param name="sender">The sender</param>
        /// <param name="syncJobProgressInfo">The progressInfo object</param>
        private void SyncJobOnProgressChanged(object sender, SyncJobProgressInfo syncJobProgressInfo)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                if (syncJobProgressInfo.Message != null)
                {
                    this.SyncProgressCurrentText = syncJobProgressInfo.Message;
                }

                if (double.IsInfinity(syncJobProgressInfo.ProgressValue))
                {
                }
                else
                {
                    this.ShowDiscreteProgress = true;

                    this.ProgressValue = syncJobProgressInfo.ProgressValue;
                    this.SyncProgressCurrentText = syncJobProgressInfo.ProgressValue.ToString("P0") + " complete";

                    this.BytesCompleted = syncJobProgressInfo.BytesCompleted;
                    this.BytesRemaining = syncJobProgressInfo.BytesTotal - syncJobProgressInfo.BytesCompleted;

                    this.FilesCompleted = syncJobProgressInfo.FilesCompleted;
                    this.FilesRemaining = syncJobProgressInfo.FilesTotal - syncJobProgressInfo.FilesCompleted;

                    this.TimeElapsed = DateTime.Now.Subtract(this.StartTime);

                    if (DateTime.Now.Subtract(this.lastSyncProgressUpdate).TotalSeconds > 1)
                    {
                        this.lastSyncProgressUpdate = DateTime.Now;

                        if (syncJobProgressInfo.BytesPerSecond > 0)
                        {
                            var secondsRemaining =
                                this.BytesRemaining / syncJobProgressInfo.BytesPerSecond;

                            this.TimeRemaining = TimeSpan.FromSeconds(
                                Math.Round(secondsRemaining / 5.0) * 5);
                        }
                        else
                        {
                            this.TimeRemaining = TimeSpan.Zero;
                        }

                        this.Throughput = FileSizeConverter.Convert(syncJobProgressInfo.BytesPerSecond, 2) + " per second";
                    }
                }

                if (!this.EntryUpdatesTreeList.Any() && syncJobProgressInfo.UpdateInfo != null)
                {
                    SyncEntry rootEntry = syncJobProgressInfo.UpdateInfo.OriginatingAdapter.GetRootSyncEntry();

                    var rootNode = new EntryUpdateInfoViewModel
                    {
                        Name = rootEntry.Name,
                        IsDirectory = true
                    };

                    this.EntryUpdatesTreeList.Add(rootNode);
                }

                if (syncJobProgressInfo.UpdateInfo != null)
                {
                    IList<string> pathStack = syncJobProgressInfo.UpdateInfo.RelativePath.Split('\\').ToList();

                    this.CalculateChangeMetrics(syncJobProgressInfo.UpdateInfo);

                    this.AddEntryUpdate(
                        new EntryUpdateInfoViewModel(
                            syncJobProgressInfo.UpdateInfo, 
                            this.SyncRelationship,
                            -1), 
                        pathStack);
                }
            });
        }

        public void CalculateChangeMetrics(EntryUpdateInfo info)
        {
            if (info.Entry.Type == SyncEntryType.Directory)
            {
                if (info.HasSyncEntryFlag(SyncEntryChangedFlags.NewDirectory))
                {
                    this.ChangeMetricsList[1].Added++;
                }
                else if (info.HasSyncEntryFlag(SyncEntryChangedFlags.Deleted))
                {
                    this.ChangeMetricsList[1].Removed++;
                }
                else if (info.HasSyncEntryFlag(SyncEntryChangedFlags.CreatedTimestamp)
                         || info.HasSyncEntryFlag(SyncEntryChangedFlags.ModifiedTimestamp))
                {
                    this.ChangeMetricsList[1].Metadata++;
                }
            }
            else
            {
                if (info.HasSyncEntryFlag(SyncEntryChangedFlags.NewFile))
                {
                    this.ChangeMetricsList[0].Added++;
                    this.ChangeMetricsList[2].Added += info.Entry.GetSize(this.SyncRelationship.GetSyncRelationship(), SyncEntryPropertyLocation.Source);
                    this.BytesToCopy += info.Entry.GetSize(this.SyncRelationship.GetSyncRelationship(), SyncEntryPropertyLocation.Source);
                }
                else if (info.HasSyncEntryFlag(SyncEntryChangedFlags.Deleted))
                {
                    this.ChangeMetricsList[0].Removed++;
                    this.ChangeMetricsList[2].Removed += info.Entry.GetSize(this.SyncRelationship.GetSyncRelationship(), SyncEntryPropertyLocation.Source);
                }
                else if (info.HasSyncEntryFlag(SyncEntryChangedFlags.CreatedTimestamp)
                         || info.HasSyncEntryFlag(SyncEntryChangedFlags.ModifiedTimestamp))
                {
                    this.ChangeMetricsList[0].Metadata++;
                }
                else if (info.HasSyncEntryFlag(SyncEntryChangedFlags.Sha1Hash)
                         || info.HasSyncEntryFlag(SyncEntryChangedFlags.Md5Hash)
                         || info.HasSyncEntryFlag(SyncEntryChangedFlags.FileSize))
                {
                    this.ChangeMetricsList[0].Modified++;
                    this.ChangeMetricsList[2].Modified += info.Entry.GetSize(this.SyncRelationship.GetSyncRelationship(), SyncEntryPropertyLocation.Source);
                    this.BytesToCopy += info.Entry.GetSize(this.SyncRelationship.GetSyncRelationship(), SyncEntryPropertyLocation.Source);
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private TimeSpan duration;

        public TimeSpan Duration
        {
            get { return this.duration; }
            set { this.SetProperty(ref this.duration, value); }
        }

        //[DebuggerBrowsable(DebuggerBrowsableState.Never)]
        //private double progressValue;

        //public double ProgressValue
        //{
        //    get { return this.progressValue; }
        //    set { this.SetProperty(ref this.progressValue, value); }
        //}

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private long filesCompleted;

        public long FilesCompleted
        {
            get { return this.filesCompleted; }
            set { this.SetProperty(ref this.filesCompleted, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private TimeSpan timeElapsed;

        public TimeSpan TimeElapsed
        {
            get { return this.timeElapsed; }
            set { this.SetProperty(ref this.timeElapsed, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private TimeSpan timeRemaining;

        public TimeSpan TimeRemaining
        {
            get { return this.timeRemaining; }
            set { this.SetProperty(ref this.timeRemaining, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private long filesRemaining;

        public long FilesRemaining
        {
            get { return this.filesRemaining; }
            set { this.SetProperty(ref this.filesRemaining, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private long bytesCompleted;

        public long BytesCompleted
        {
            get { return this.bytesCompleted; }
            set { this.SetProperty(ref this.bytesCompleted, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private long bytesRemaining;

        public long BytesRemaining
        {
            get { return this.bytesRemaining; }
            set { this.SetProperty(ref this.bytesRemaining, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string throughput;

        public string Throughput
        {
            get { return this.throughput; }
            set { this.SetProperty(ref this.throughput, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool showDiscreteProgress;

        public bool ShowDiscreteProgress
        {
            get { return this.showDiscreteProgress; }
            set { this.SetProperty(ref this.showDiscreteProgress, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string syncProgressCurrentText;

        // The text shown in the middle of the progress bar (e.g. 65% complete)
        public string SyncProgressCurrentText
        {
            get { return this.syncProgressCurrentText; }
            set { this.SetProperty(ref this.syncProgressCurrentText, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string itemsCopiedDisplayString;

        // Number of items updated? (only used for completed jobs)
        public string ItemsCopiedDisplayString
        {
            get { return this.itemsCopiedDisplayString; }
            set { this.SetProperty(ref this.itemsCopiedDisplayString, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string statusDescription;

        // Overall description (only used for completed jobs)
        public string StatusDescription
        {
            get { return this.statusDescription; }
            set { this.SetProperty(ref this.statusDescription, value); }
        }

        public List<ChangeMetrics> ChangeMetricsList { get; }

        public long BytesToCopy { get; set; }

        private ObservableCollection<EntryUpdateInfoViewModel> entryUpdatesTreeList;

        public ObservableCollection<EntryUpdateInfoViewModel> EntryUpdatesTreeList => 
            this.entryUpdatesTreeList ?? (this.entryUpdatesTreeList = new ObservableCollection<EntryUpdateInfoViewModel>());

        private ObservableCollection<EntryUpdateInfoViewModel> entryUpdatesFlatList;

        public ObservableCollection<EntryUpdateInfoViewModel> EntryUpdatesFlatList
            => this.entryUpdatesFlatList ?? (this.entryUpdatesFlatList = new ObservableCollection<EntryUpdateInfoViewModel>());

        private void AddEntryUpdate(EntryUpdateInfoViewModel viewModel, IList<string> pathStack)
        {
            // Add the view model to the flat list
            App.DispatcherInvoke(() => { this.EntryUpdatesFlatList.Add(viewModel); });

            EntryUpdateInfoViewModel rootNode = this.EntryUpdatesTreeList.First();

            this.InsertTreeViewNode(viewModel, rootNode, pathStack);
        }

        private void InsertTreeViewNode(EntryUpdateInfoViewModel entry, EntryUpdateInfoViewModel node, IList<string> pathStack)
        {
            if (pathStack.Count < 1)
            {
                throw new InvalidOperationException("Skipped something in the path stack");
            }

            if (pathStack.Count == 1)
            {
                App.DispatcherInvoke(() => { node.ChildEntries.Add(entry); });
                return;
            }

            EntryUpdateInfoViewModel childNode = node.ChildEntries.FirstOrDefault(e => e.Name == pathStack.First());
            if (childNode == null)
            {
                // No root element for this entry
                childNode = new EntryUpdateInfoViewModel { Name = pathStack.First(), NoChange = true, IsDirectory = true };
                App.DispatcherInvoke(() => { node.ChildEntries.Add(childNode); });
            }

            pathStack.RemoveAt(0);
            this.InsertTreeViewNode(entry, childNode, pathStack);
        }
    }
}