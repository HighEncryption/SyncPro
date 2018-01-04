namespace SyncPro.UI.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
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
    using SyncPro.UI.Framework;
    using SyncPro.UI.Framework.MVVM;
    using SyncPro.UI.Navigation;
    using SyncPro.UI.Navigation.ViewModels;

    public enum SyncRunChangesDisplayMode
    {
        OnlyChanges,
        AllFiles,
    }

    public class SyncRunViewModel : ViewModelBase, IRequestClose
    {
        public SyncRun SyncRun { get; }
        private bool loadFromHistory;

        public ICommand CloseWindowCommand { get; }

        public ICommand ViewSyncRunCommand { get; }

        public ICommand BeginSyncRunCommand { get; }

        public ICommand CancelSyncRunCommand { get; }

        public SyncRelationshipViewModel SyncRelationship { get; }

        public SyncRunViewModel(SyncRun syncRun, SyncRelationshipViewModel relationshipViewModel, bool loadFromHistory)
        {
            this.SyncRun = syncRun;
            this.SyncRelationship = relationshipViewModel;
            this.loadFromHistory = loadFromHistory;

            this.SyncRun.SyncStarted += this.SyncRunOnSyncStarted;
            this.SyncRun.SyncFinished += this.SyncRunOnSyncFinished;
            this.SyncRun.ProgressChanged += this.SyncRunOnProgressChanged;

            this.CloseWindowCommand = new DelegatedCommand(o => this.HandleClose(false));
            this.ViewSyncRunCommand = new DelegatedCommand(o => this.ViewSyncRun());
            this.BeginSyncRunCommand = new DelegatedCommand(o => this.BeginSyncRun());
            this.CancelSyncRunCommand = new DelegatedCommand(o => this.CancelSyncRun());

            this.ChangeMetricsList = new List<ChangeMetrics>()
            {
                new ChangeMetrics("Files"),
                new ChangeMetrics("Folders"),
                new ChangeMetrics("Bytes", true)
            };

            if (syncRun.HasStarted)
            {
                this.StartTime = syncRun.StartTime;
            }

            if (syncRun.HasFinished)
            {
                this.EndTime = syncRun.EndTime ?? DateTime.MinValue;
                this.ItemsCopiedDisplayString = String.Format(
                    "{0} files / {1}",
                    this.SyncRun.FilesTotal,
                    FileSizeConverter.Convert(this.SyncRun.BytesTotal, 1));
                this.Duration = this.EndTime - this.StartTime;
                this.SetStatusDescription();
            }

            if (this.SyncRelationship.State == SyncRelationshipState.Running)
            {
                this.SyncRunOnSyncStarted(this, new EventArgs());
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
                                .Where(e => e.SyncHistoryId == this.SyncRun.Id)
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

        private void ViewSyncRun()
        {
            // Find the navigation tree item for this relationship
            SyncRelationshipNodeViewModel relatonshipNavItem =
                App.Current.MainWindowsViewModel.NavigationItems.OfType<SyncRelationshipNodeViewModel>().FirstOrDefault(
                    n => n.Item == this.SyncRelationship);

            Debug.Assert(relatonshipNavItem != null, "relatonshipNavItem != null");

            SyncHistoryNodeViewModel syncHistoryNode =
                relatonshipNavItem.Children.OfType<SyncHistoryNodeViewModel>().First();

            // Check if a Sync History item is already present under this relationship for this history
            foreach (SyncRunNodeViewModel syncRunNode in syncHistoryNode.Children.OfType<SyncRunNodeViewModel>())
            {
                var panelViewModel = syncRunNode.Item as SyncRunPanelViewModel;
                if (panelViewModel != null && panelViewModel.SyncRun == this)
                {
                    syncRunNode.IsSelected = true;
                    return;
                }
            }
        }

        private void BeginSyncRun()
        {
            this.SyncRun.Start(SyncTriggerType.Manual);
        }

        private void CancelSyncRun()
        {
            if (this.SyncRun.HasStarted)
            {
                this.SyncRun.Cancel();
            }
        }

        private void SetStatusDescription()
        {
            switch (this.SyncRun.SyncResult)
            {
                case SyncRunResult.Undefined:
                    this.StatusDescription = "Unknown";
                    break;
                case SyncRunResult.Success:
                    this.StatusDescription = "Finished Successfully";
                    break;
                case SyncRunResult.Warning:
                    this.StatusDescription = "Finished With Warnings";
                    break;
                case SyncRunResult.Error:
                    this.StatusDescription = "Failed";
                    break;
                case SyncRunResult.NotRun:
                    this.StatusDescription = "Sync Not Run";
                    break;
                case SyncRunResult.Cancelled:
                    this.StatusDescription = "Cancelled";
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public SyncRunResult SyncRunResult => this.SyncRun.SyncResult;

        private void SyncRunOnSyncFinished(object sender, EventArgs eventArgs)
        {
            this.EndTime = this.SyncRun.EndTime.Value;
            this.ItemsCopiedDisplayString = 
                string.Format(
                    "{0} files / {1}", 
                    this.SyncRun.FilesTotal, 
                    FileSizeConverter.Convert(this.SyncRun.BytesTotal, 1));

            // Calculate unchanges files and folders
            this.ChangeMetricsList[0].Unchanged = this.SyncRun.AnalyzeResult.UnchangedFileCount;
            this.ChangeMetricsList[1].Unchanged = this.SyncRun.AnalyzeResult.UnchangedFolderCount;
            this.ChangeMetricsList[2].Unchanged = this.SyncRun.AnalyzeResult.UnchangedFileBytes;

            this.SetStatusDescription();

            this.metadataUpdateCancellationToken.Cancel();
        }

        private CancellationTokenSource metadataUpdateCancellationToken;

        private void SyncRunOnSyncStarted(object sender, EventArgs eventArgs)
        {
            this.StartTime = this.SyncRun.StartTime;
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

        /// <summary>
        /// During an active sync run, this methods is invoked on progress changed (aka when a change is detected).
        /// </summary>
        /// <param name="sender">The sender</param>
        /// <param name="syncRunProgressInfo">The progressInfo object</param>
        private void SyncRunOnProgressChanged(object sender, SyncRunProgressInfo syncRunProgressInfo)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                this.Stage = syncRunProgressInfo.Stage;

                if (syncRunProgressInfo.Message != null)
                {
                    this.SyncProgressCurrentText = syncRunProgressInfo.Message;
                }

                if (syncRunProgressInfo.Stage == SyncRunStage.Sync)
                {
                    if (double.IsInfinity(syncRunProgressInfo.ProgressValue))
                    {
                    }
                    else
                    {
                        this.ShowDiscreteProgress = true;

                        this.ProgressValue = syncRunProgressInfo.ProgressValue;
                        this.SyncProgressCurrentText = syncRunProgressInfo.ProgressValue.ToString("P0") + " complete";

                        this.BytesCompleted = syncRunProgressInfo.BytesCompleted;
                        this.BytesRemaining = syncRunProgressInfo.BytesTotal - syncRunProgressInfo.BytesCompleted;

                        this.FilesCompleted = syncRunProgressInfo.FilesCompleted;
                        this.FilesRemaining = syncRunProgressInfo.FilesTotal - syncRunProgressInfo.FilesCompleted;

                        this.TimeElapsed = DateTime.Now.Subtract(this.StartTime);

                        if (syncRunProgressInfo.BytesPerSecond > 0)
                        {
                            this.TimeRemaining = TimeSpan.FromSeconds(
                                this.BytesRemaining / syncRunProgressInfo.BytesPerSecond);
                        }
                        else
                        {
                            this.TimeRemaining = TimeSpan.Zero;
                        }

                        this.Throughput = FileSizeConverter.Convert(syncRunProgressInfo.BytesPerSecond, 2) + " per second";
                    }
                }
                else if (syncRunProgressInfo.Stage == SyncRunStage.Analyze)
                {
                    this.AnalyzeStatusString =
                        string.Format("Analyzing. Found {0} changes", syncRunProgressInfo.FilesTotal);
                }

                if (!this.EntryUpdatesTreeList.Any() && syncRunProgressInfo.UpdateInfo != null)
                {
                    SyncEntry rootEntry = syncRunProgressInfo.UpdateInfo.OriginatingAdapter.GetRootSyncEntry();

                    var rootNode = new EntryUpdateInfoViewModel
                    {
                        Name = rootEntry.Name,
                        IsDirectory = true
                    };

                    this.EntryUpdatesTreeList.Add(rootNode);
                }

                // 10/26: Why did we care about the stage??
                //if (syncRunProgressInfo.Stage == SyncRunStage.Sync && syncRunProgressInfo.UpdateInfo != null)
                if (syncRunProgressInfo.UpdateInfo != null)
                {
                    IList<string> pathStack = syncRunProgressInfo.UpdateInfo.RelativePath.Split('\\').ToList();

                    this.CalculateChangeMetrics(syncRunProgressInfo.UpdateInfo);

                    this.AddEntryUpdate(
                        new EntryUpdateInfoViewModel(syncRunProgressInfo.UpdateInfo, this.SyncRelationship), 
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

        private void HandleClose(bool dialogResult)
        {
            this.RequestClose?.Invoke(this, new RequestCloseEventArgs(dialogResult));
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private DateTime startTime;

        public DateTime StartTime
        {
            get { return this.startTime; }
            set { this.SetProperty(ref this.startTime, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private DateTime endTime;

        public DateTime EndTime
        {
            get { return this.endTime; }
            set { this.SetProperty(ref this.endTime, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private TimeSpan duration;

        public TimeSpan Duration
        {
            get { return this.duration; }
            set { this.SetProperty(ref this.duration, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private SyncRunStage stage;

        public SyncRunStage Stage
        {
            get { return this.stage; }
            set { this.SetProperty(ref this.stage, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private double progressValue;

        public double ProgressValue
        {
            get { return this.progressValue; }
            set { this.SetProperty(ref this.progressValue, value); }
        }

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

        // Number of items updated? (only used for completed runs)
        public string ItemsCopiedDisplayString
        {
            get { return this.itemsCopiedDisplayString; }
            set { this.SetProperty(ref this.itemsCopiedDisplayString, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string statusDescription;

        // Overall description (only used for completed runs)
        public string StatusDescription
        {
            get { return this.statusDescription; }
            set { this.SetProperty(ref this.statusDescription, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string analyzeStatusString;

        public string AnalyzeStatusString
        {
            get { return this.analyzeStatusString; }
            set { this.SetProperty(ref this.analyzeStatusString, value); }
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

        #region IRequestClose

        public event RequestCloseEventHandler RequestClose;

        public void WindowClosing(CancelEventArgs e)
        {
            if (this.MustClose)
            {
                // We are being forced to close, so don't show the confirmation message.
                e.Cancel = false;
            }
        }

        public bool MustClose { get; set; }

        public bool IsAnalyzeOnly => this.SyncRun.AnalyzeOnly;

        #endregion IRequestClose
    }
}