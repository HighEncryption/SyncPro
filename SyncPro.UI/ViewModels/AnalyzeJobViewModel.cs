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
    using SyncPro.UI.Framework;

    public class ErrorViewModel
    {
        public bool IsWarning { get; }
        public string Header { get; }
        public string Message { get; }

        public ErrorViewModel(
            bool isWarning,
            string header,
            string message)
        {
            this.IsWarning = isWarning;
            Header = header;
            Message = message;
        }
    }

    public enum ProgressState
    {
        None,
        Warning,
        Error
    }

    public class AnalyzeJobViewModel : JobViewModel
    {
        public AnalyzeJob AnalyzeJob => (AnalyzeJob) this.Job;

        public List<ChangeMetrics> ChangeMetricsList { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string analyzeStatusString;

        public string AnalyzeStatusString
        {
            get => this.analyzeStatusString;
            set => this.SetProperty(ref this.analyzeStatusString, value);
        }

        private ObservableCollection<ErrorViewModel> errorMessages;

        public ObservableCollection<ErrorViewModel> ErrorMessages =>
            this.errorMessages ?? (this.errorMessages = new ObservableCollection<ErrorViewModel>());

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
            this.ProgressValue = 0;
            this.IsProgressIndeterminate = true;

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

            this.ProgressValue = 1;
            this.IsProgressIndeterminate = false;

            // Calculate unchanged files and folders
            this.ChangeMetricsList[0].Unchanged = this.AnalyzeJob.AnalyzeResult.UnchangedFileCount;
            this.ChangeMetricsList[1].Unchanged = this.AnalyzeJob.AnalyzeResult.UnchangedFolderCount;
            this.ChangeMetricsList[2].Unchanged = this.AnalyzeJob.AnalyzeResult.UnchangedFileBytes;

            int entryErrorCount = 0;
            foreach (KeyValuePair<int, AnalyzeAdapterResult> adapterResult in this.AnalyzeJob.AnalyzeResult.AdapterResults)
            {
                if (adapterResult.Value.Exception != null)
                {
                    App.DispatcherInvoke(() =>
                    {
                        this.ErrorMessages.Add(
                            new ErrorViewModel(
                                false,
                                adapterResult.Value.Exception.Message,
                                adapterResult.Value.Exception.ToString()));
                    });

                    this.ProgressState = ProgressState.Error;
                }

                entryErrorCount +=
                    adapterResult.Value.EntryResults.Count(e => e.HasSyncEntryFlag(SyncEntryChangedFlags.Exception));
            }

            if (entryErrorCount > 0)
            {
                var errorViewModel = new ErrorViewModel(
                    false,
                    string.Format(
                        "Error were encountered while analyzing {0} items. These items will not be synchronized.",
                        entryErrorCount),
                    null);

                App.DispatcherInvoke(() => this.ErrorMessages.Add(errorViewModel));
            }

            //// Performance of this call isn't great, but there shouldn't be a large number of exceptions
            //// to process in this way.
            //List<Exception> exceptions = 
            //    this.AnalyzeJob.AnalyzeResult.AdapterResults
            //        .Where(r => r.Value.Exception != null)
            //        .Select(r => r.Value.Exception)
            //        .ToList();

            //if (exceptions.Any())
            //{
            //    foreach (Exception exception in exceptions)
            //    {
            //        this.ErrorMessages.Add(
            //            new ErrorViewModel(
            //                false,
            //                exception.Message,
            //                exception.ToString()));
            //    }

            //    this.ProgressState = ProgressState.Error;
            //}

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
                if (progressInfo.Activity != null)
                {
                    this.AnalyzeStatusString = progressInfo.Activity;
                }
                else
                {
                    this.AnalyzeStatusString =
                        string.Format("Analyzing. Found {0} changes", progressInfo.FilesTotal);
                }

                if (progressInfo.ProgressValue == null)
                {
                    this.IsProgressIndeterminate = true;
                }
                else
                {
                    this.IsProgressIndeterminate = false;
                    this.ProgressValue = progressInfo.ProgressValue.Value;
                }

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
                        new EntryUpdateInfoViewModel(
                            progressInfo.UpdateInfo, 
                            this.SyncRelationship,
                            progressInfo.SourceAdapterId),
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
                    if (info.HasSyncEntryFlag(SyncEntryChangedFlags.DestinationExists))
                    {
                        this.ChangeMetricsList[1].Existing++;
                    }
                    else
                    {
                        this.ChangeMetricsList[1].Added++;
                    }
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
                    if (info.HasSyncEntryFlag(SyncEntryChangedFlags.DestinationExists))
                    {
                        this.ChangeMetricsList[0].Existing++;
                        this.ChangeMetricsList[2].Existing += info.Entry.GetSize(this.SyncRelationship.GetSyncRelationship(), SyncEntryPropertyLocation.Source);
                    }
                    else
                    {
                        this.ChangeMetricsList[0].Added++;
                        this.ChangeMetricsList[2].Added += info.Entry.GetSize(this.SyncRelationship.GetSyncRelationship(), SyncEntryPropertyLocation.Source);
                        this.BytesToCopy += info.Entry.GetSize(this.SyncRelationship.GetSyncRelationship(), SyncEntryPropertyLocation.Source);
                    }
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
            this.AnalyzeJob.ProgressChanged += this.SyncJobOnProgressChanged;

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