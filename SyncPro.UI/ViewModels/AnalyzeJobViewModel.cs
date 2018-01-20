namespace SyncPro.UI.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Input;

    using SyncPro.Adapters;
    using SyncPro.Data;
    using SyncPro.Runtime;

    public class AnalyzeJobViewModel : JobViewModel
    {
        public AnalyzeJob AnalyzeJob => (AnalyzeJob) this.Job;

        public List<ChangeMetrics> ChangeMetricsList { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string analyzeStatusString;

        public string AnalyzeStatusString
        {
            get { return this.analyzeStatusString; }
            set { this.SetProperty(ref this.analyzeStatusString, value); }
        }

        private CancellationTokenSource metadataUpdateCancellationToken;

        private bool startedProcessing;
        private volatile object startLock = new object();

        private void OnJobStarted(object sender, EventArgs eventArgs)
        {
            if (!this.startedProcessing)
            {
                lock (this.startLock)
                {
                    if (!this.startedProcessing)
                    {
                        App.DispatcherInvoke(CommandManager.InvalidateRequerySuggested);

                        this.StartInternal();
                        this.startedProcessing = true;
                    }
                }
            }
        }

        private void StartInternal()
        {
            this.StartTime = this.AnalyzeJob.StartTime;
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

        private void OnJobFinished(object sender, EventArgs eventArgs)
        {
            this.EndTime = this.AnalyzeJob.EndTime.Value;

            // Calculate unchanges files and folders
            this.ChangeMetricsList[0].Unchanged = this.AnalyzeJob.AnalyzeResult.UnchangedFileCount;
            this.ChangeMetricsList[1].Unchanged = this.AnalyzeJob.AnalyzeResult.UnchangedFolderCount;
            this.ChangeMetricsList[2].Unchanged = this.AnalyzeJob.AnalyzeResult.UnchangedFileBytes;

            this.metadataUpdateCancellationToken.Cancel();

            App.DispatcherInvoke(CommandManager.InvalidateRequerySuggested);
        }

        /// <summary>
        /// During an active sync job, this methods is invoked on progress changed (aka when a change is detected).
        /// </summary>
        /// <param name="sender">The sender</param>
        /// <param name="progressInfo">The progressInfo object</param>
        private void SyncJobOnProgressChanged(object sender, AnalyzeJobProgressInfo progressInfo)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                this.AnalyzeStatusString =
                    string.Format("Analyzing. Found {0} changes", progressInfo.FilesTotal);

                if (!this.EntryUpdatesTreeList.Any() && progressInfo.UpdateInfo != null)
                {
                    SyncEntry rootEntry = progressInfo.UpdateInfo.OriginatingAdapter.GetRootSyncEntry();

                    var rootNode = new EntryUpdateInfoViewModel
                    {
                        Name = rootEntry.Name,
                        IsDirectory = true
                    };

                    this.EntryUpdatesTreeList.Add(rootNode);
                }

                // 10/26: Why did we care about the stage??
                //if (syncJobProgressInfo.Stage == SyncJobStage.Sync && syncJobProgressInfo.UpdateInfo != null)
                if (progressInfo.UpdateInfo != null)
                {
                    IList<string> pathStack = progressInfo.UpdateInfo.RelativePath.Split('\\').ToList();

                    this.CalculateChangeMetrics(progressInfo.UpdateInfo);

                    this.AddEntryUpdate(
                        new EntryUpdateInfoViewModel(progressInfo.UpdateInfo, this.SyncRelationship),
                        pathStack);
                }
            });
        }

        public void CalculateChangeMetrics(EntryUpdateInfo info)
        {
            if (info.Entry.Type == SyncEntryType.Directory)
            {
                if (info.HasSyncEntryFlag(SyncEntryChangedFlags.NewDirectory) ||
                    info.HasSyncEntryFlag(SyncEntryChangedFlags.DirectoryExists))
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
                if (info.HasSyncEntryFlag(SyncEntryChangedFlags.NewFile) ||
                    info.HasSyncEntryFlag(SyncEntryChangedFlags.FileExists))
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

        public AnalyzeJobViewModel(AnalyzeJob job, SyncRelationshipViewModel relationshipViewModel)
            : base(job, relationshipViewModel)
        {
            this.AnalyzeJob.Started += this.OnJobStarted;
            this.AnalyzeJob.Finished += this.OnJobFinished;
            this.AnalyzeJob.ChangeDetected += this.SyncJobOnProgressChanged;

            this.ChangeMetricsList = new List<ChangeMetrics>()
            {
                new ChangeMetrics("Files"),
                new ChangeMetrics("Folders"),
                new ChangeMetrics("Bytes", true)
            };

            if (job.HasStarted)
            {
                this.StartTime = job.StartTime;
            }

            if (this.AnalyzeJob.HasStarted)
            {
                this.OnJobStarted(this, new EventArgs());
            }
        }
    }
}