namespace SyncPro.UI.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using System.Windows.Input;

    using SyncPro.Adapters;
    using SyncPro.Configuration;
    using SyncPro.Data;
    using SyncPro.Runtime;
    using SyncPro.Tracing;
    using SyncPro.UI.Converters;
    using SyncPro.UI.Framework;
    using SyncPro.UI.Framework.MVVM;
    using SyncPro.UI.Navigation;
    using SyncPro.UI.Navigation.MenuCommands;
    using SyncPro.UI.Navigation.ViewModels;
    using SyncPro.UI.RelationshipEditor;
    using SyncPro.UI.Utility;
    using SyncPro.UI.ViewModels.Adapters;

    public class SyncRelationshipViewModel : ViewModelBase<SyncRelationship>
    {
        // Gets a value indicating whether this sync relationship has been created (some properties cannot be edited once created).
        public bool IsCreated => this.BaseModel.Configuration.InitiallyCreatedUtc != DateTime.MinValue;

        public ICommand EditRelationshipCommand { get; }

        public ICommand SyncNowCommand { get; }

        public ICommand AnalyzeNowCommand { get; }

        public ICommand DeleteRelationshipCommand { get; }

        private Dictionary<string, string> baseModelPropertyNames;

        #region Properties exposed from the base model

        [BaseModelProperty(NotifyOnPropertyChange = true)]
        public string Name
        {
            get { return this.BaseModel.Name; }
            set { this.BaseModel.Name = value; }
        }

        [BaseModelProperty(NotifyOnPropertyChange = true)]
        public string Description
        {
            get { return this.BaseModel.Description; }
            set { this.BaseModel.Description = value; }
        }

        [BaseModelProperty(NotifyOnPropertyChange = true)]
        public SyncScopeType Scope
        {
            get { return this.BaseModel.SyncScope; }
            set { this.BaseModel.SyncScope = value; }
        }

        [BaseModelProperty(NotifyOnPropertyChange = true)]
        public SyncTriggerType TriggerType
        {
            get { return this.BaseModel.TriggerType; }
            set { this.BaseModel.TriggerType = value; }
        }

        [BaseModelProperty(NotifyOnPropertyChange = true)]
        public int TriggerHourlyInterval
        {
            get { return this.BaseModel.TriggerHourlyInterval; }
            set { this.BaseModel.TriggerHourlyInterval = value; }
        }

        [BaseModelProperty(NotifyOnPropertyChange = true)]
        public SyncRelationshipState State => this.BaseModel.State;

        [BaseModelProperty(NotifyOnPropertyChange = true)]
        public bool IsThrottlingEnabled
        {
            get { return this.BaseModel.IsThrottlingEnabled; }
            set { this.BaseModel.IsThrottlingEnabled = value; }
        }

        [BaseModelProperty(NotifyOnPropertyChange = true)]
        public int ThrottlineValue
        {
            get { return this.BaseModel.ThrottlingValue; }
            set { this.BaseModel.ThrottlingValue = value; }
        }

        [BaseModelProperty(NotifyOnPropertyChange = true)]
        public int ThrottlingScaleFactor
        {
            get { return this.BaseModel.ThrottlingScaleFactor; }
            set { this.BaseModel.ThrottlingScaleFactor = value; }
        }

        [BaseModelProperty(NotifyOnPropertyChange = true)]
        public EncryptionMode EncryptionMode
        {
            get { return this.BaseModel.EncryptionMode; }
            set { this.BaseModel.EncryptionMode = value; }
        }

        [BaseModelProperty(NotifyOnPropertyChange = true)]
        public bool EncryptionCreateCertificate
        {
            get { return this.BaseModel.EncryptionCreateCertificate; }
            set { this.BaseModel.EncryptionCreateCertificate = value; }
        }

        #endregion

        #region ViewModel properties

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private ISyncTargetViewModel syncSourceAdapter;

        public ISyncTargetViewModel SyncSourceAdapter
        {
            get { return this.syncSourceAdapter; }
            set
            {
                ISyncTargetViewModel oldValue = this.syncSourceAdapter;
                if (this.SetProperty(ref this.syncSourceAdapter, value))
                {
                    // Remove the previous adapter from the model
                    if (oldValue != null)
                    {
                        this.BaseModel.Adapters.Remove(oldValue.AdapterBase);
                    }

                    if (value != null &&
                        !this.BaseModel.Adapters.Contains(value.AdapterBase))
                    {
                        this.BaseModel.Adapters.Add(value.AdapterBase);
                        this.BaseModel.SourceAdapter = value.AdapterBase;
                    }
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private ISyncTargetViewModel syncDestinationAdapter;

        public ISyncTargetViewModel SyncDestinationAdapter
        {
            get { return this.syncDestinationAdapter; }
            set
            {
                ISyncTargetViewModel oldValue = this.syncDestinationAdapter;
                if (this.SetProperty(ref this.syncDestinationAdapter, value))
                {
                    if (oldValue != null)
                    {
                        this.BaseModel.Adapters.Remove(oldValue.AdapterBase);
                    }

                    if (value != null &&
                        !this.BaseModel.Adapters.Contains(value.AdapterBase))
                    {
                        this.BaseModel.Adapters.Add(value.AdapterBase);
                        this.BaseModel.DestinationAdapter = value.AdapterBase;
                    }
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string syncStatusDescription;

        public string SyncStatusDescription
        {
            get { return this.syncStatusDescription; }
            set { this.SetProperty(ref this.syncStatusDescription, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool isNeverSynchronized;

        public bool IsNeverSynchronized
        {
            get { return this.isNeverSynchronized; }
            set { this.SetProperty(ref this.isNeverSynchronized, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string lastSyncDisplayString;

        public string LastSyncDisplayString
        {
            get { return this.lastSyncDisplayString; }
            set { this.SetProperty(ref this.lastSyncDisplayString, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string nextSyncDisplayString;

        public string NextSyncDisplayString
        {
            get { return this.nextSyncDisplayString; }
            set { this.SetProperty(ref this.nextSyncDisplayString, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string relationshipSizeDisplayString;

        public string RelationshipSizeDisplayString
        {
            get { return this.relationshipSizeDisplayString; }
            set { this.SetProperty(ref this.relationshipSizeDisplayString, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string databaseSizeDisplayString;

        public string DatabaseSizeDisplayString
        {
            get { return this.databaseSizeDisplayString; }
            set { this.SetProperty(ref this.databaseSizeDisplayString, value); }
        }

        #endregion

        #region Properties for sync progress display

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private JobViewModel activeJob;

        public JobViewModel ActiveJob
        {
            get { return this.activeJob; }
            private set { this.SetProperty(ref this.activeJob, value); }
        }

        private ObservableCollection<ViewModelBase> errors;

        public ObservableCollection<ViewModelBase> Errors
            => this.errors ?? (this.errors = new ObservableCollection<ViewModelBase>());

        public event EventHandler<JobFinishedEventArgs> JobFinished;

        #endregion

        private void UpdateStatusDescription()
        {
            JobViewModel lastSyncJob = this.SyncJobHistory.OrderBy(h => h.StartTime).LastOrDefault();

            if (lastSyncJob == null)
            {
                this.SyncStatusDescription = "Never Synchronized";
                this.LastSyncDisplayString = "Never Synchronized";
                this.IsNeverSynchronized = true;
            }
            else
            {
                this.IsNeverSynchronized = false;
                this.LastSyncDisplayString = lastSyncJob.StartTime.ToString("g");

                if (lastSyncJob.Job.JobResult == JobResult.Warning)
                {
                    this.SyncStatusDescription = string.Format(
                        "Last synchronized on {0} with warnings",
                        lastSyncJob.StartTime);
                }
                else if (lastSyncJob.Job.JobResult == JobResult.Error)
                {
                    this.SyncStatusDescription = string.Format(
                        "Last synchronized on {0} with errors",
                        lastSyncJob.StartTime);
                }
                else
                {
                    this.SyncStatusDescription = "Idle";
                }
            }
        }

        public SyncRelationshipViewModel(SyncRelationship relationship, bool loadContext) 
            : base(relationship)
        {
            this.InitializeBaseModelProperties();

            this.EditRelationshipCommand = new DelegatedCommand(this.EditRelationship, this.CanEditRelationship);
            this.SyncNowCommand = new DelegatedCommand(o => this.StartSyncJob(SyncTriggerType.Manual), this.CanSyncNow);
            this.AnalyzeNowCommand = new DelegatedCommand(o => this.StartAnalyzeJob(o), this.CanSyncNow);
            this.DeleteRelationshipCommand = new DelegatedCommand(this.DeleteRelationship, this.CanDeleteRelationship);

            if (loadContext)
            {
                this.LoadContext();
            }

            this.UpdateStatusDescription();

            this.BaseModel.JobStarted += this.HandleJobStarted;
            this.BaseModel.JobFinished += this.HandleJobFinished;

            this.BaseModel.PropertyChanged += (sender, args) =>
            {
                string localPropertyName;
                if (this.baseModelPropertyNames.TryGetValue(args.PropertyName, out localPropertyName))
                {
                    this.RaisePropertyChanged(localPropertyName);
                }
            };
        }

        private void HandleJobStarted(object sender, JobStartedEventArgs args)
        {
            if (this.ActiveJob != null && this.ActiveJob.Job == args.Job)
            {
                return;
            }

            SyncJob syncJob = args.Job as SyncJob;
            if (syncJob != null)
            {
                this.ActiveJob = new SyncJobViewModel(syncJob, this, false);
            }

            AnalyzeJob analyzeJob = args.Job as AnalyzeJob;
            if (analyzeJob != null)
            {
                this.ActiveJob = new AnalyzeJobViewModel(analyzeJob, this);
            }

            RestoreJob restoreJob = args.Job as RestoreJob;
            if (restoreJob != null)
            {
                this.ActiveJob = new RestoreJobViewModel(restoreJob, this, false);
            }

            this.UpdateStatusDescription();
        }

        private void HandleJobFinished(object sender, JobFinishedEventArgs args)
        {
            // If a sync job finished, it should match the current sync job view model
            Debug.Assert(this.ActiveJob.Job == args.Job, "this.ActiveJob.Job == args.Job");

            if (this.ActiveJob.Job is SyncJob || this.ActiveJob.Job is RestoreJob)
            {
                App.DispatcherInvoke(() => { this.SyncJobHistory.Insert(0, this.ActiveJob); });
            }

            this.UpdateStatusDescription();

            this.JobFinished?.Invoke(sender, args);

            this.ActiveJob = null;
        }

        private void InitializeBaseModelProperties()
        {
            this.baseModelPropertyNames = new Dictionary<string, string>();

            foreach (PropertyInfo prop in this.GetType().GetProperties())
            {
                foreach (object objAttr in prop.GetCustomAttributes(typeof(BaseModelPropertyAttribute), true))
                {
                    BaseModelPropertyAttribute attr = objAttr as BaseModelPropertyAttribute;
                    if (attr != null)
                    {
                        // Set the local property name from the name of the property where the attribute is declared
                        attr.LocalPropertyName = prop.Name;

                        if (string.IsNullOrEmpty(attr.BasePropertyName))
                        {
                            attr.BasePropertyName = prop.Name;
                        }

                        if (attr.NotifyOnPropertyChange)
                        {
                            this.baseModelPropertyNames.Add(attr.BasePropertyName, attr.LocalPropertyName);
                        }
                    }
                }
            }
        }

        private ObservableCollection<JobViewModel> syncJobHistory;

        public ObservableCollection<JobViewModel> SyncJobHistory
            => this.syncJobHistory ?? (this.syncJobHistory = new ObservableCollection<JobViewModel>());

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private JobViewModel selectedJob;

        public JobViewModel SelectedJob
        {
            get { return this.selectedJob; }
            set
            {
                if (this.SetProperty(ref this.selectedJob, value))
                {
                    NavigationNodeViewModel selectedNavItem =
                        App.Current.MainWindowsViewModel.SelectedNavigationItem;

                    // Verify that the selected navigation item is for this view model
                    if (selectedNavItem.Item != this)
                    {
                        return;
                    }

                    selectedNavItem.MenuCommands.Clear();

                    if (value == null)
                    {
                        return;
                    }

                    selectedNavItem.MenuCommands.Add(new ViewJobMenuCommand(value));
                }
            }
        }

        private void EditRelationship(object obj)
        {
            RelationshipEditorViewModel viewModel = new RelationshipEditorViewModel(this, true);
            EditorWindow editorWindow = new EditorWindow { DataContext = viewModel };

            editorWindow.ShowDialog();

            // Update the name property.. do we want to update anything else?
        }

        private bool CanEditRelationship(object obj)
        {
            return this.ActiveJob == null;
        }

        public AnalyzeJob StartAnalyzeJob(object obj)
        {
            bool startJob = true;
            if (obj is bool)
            {
                startJob = (bool) obj;
            }

            var newAnalyzeJob = this.BaseModel.BeginAnalyzeJob(startJob);

            this.BaseModel.ActiveAnalyzeJob = newAnalyzeJob;

            return newAnalyzeJob;
        }

        private bool CanSyncNow(object obj)
        {
            return this.ActiveJob == null;
        }

        public void StartSyncJob(SyncTriggerType triggerType)
        {
            this.BaseModel.BeginSyncJob(triggerType, null);
        }

        public void RestoreFilesAsync(IList<SyncEntryViewModel> syncEntries, string restorePath)
        {
            this.BaseModel.BeginRestoreJob(syncEntries.Select(e => e.SyncEntry).ToList(), restorePath);
        }

        /// <summary>
        /// Start a new manually triggered sync job to actually sync files, using a previously gathered set of changes.
        /// </summary>
        /// <param name="previousResult">The previously gathered set of changes to be synced</param>
        public void StartSyncJob(AnalyzeRelationshipResult previousResult)
        {
            this.BaseModel.BeginSyncJob(SyncTriggerType.Manual, previousResult);
        }

        private void DeleteRelationship(object obj)
        {
            if (!this.CanDeleteRelationship(obj))
            {
                throw new InvalidOperationException("Relationship cannot be deleted");
            }

            Global.SyncRelationships.Remove(this.BaseModel);
            App.Current.MainWindowsViewModel.SyncRelationships.Remove(this);
            SyncRelationshipNodeViewModel navigationNode = App.Current.MainWindowsViewModel.NavigationItems
                .OfType<SyncRelationshipNodeViewModel>()
                .FirstOrDefault(i => i.Relationship == this);

            if (navigationNode != null)
            {
                App.Current.MainWindowsViewModel.NavigationItems.Remove(navigationNode);
            }

            this.BaseModel.Delete();
        }

        private bool CanDeleteRelationship(object obj)
        {
            return this.ActiveJob == null;
        }

        public SyncDatabase GetDatabase()
        {
            return this.BaseModel.GetDatabase();
        }

        internal SyncRelationship GetSyncRelationship()
        {
            return this.BaseModel;
        }

        #region Context

        public sealed override void LoadContext()
        {
            AdapterBase sourceAdapter = this.BaseModel.Adapters.First(a => a.Configuration.Id == this.BaseModel.Configuration.SourceAdapterId);
            AdapterBase destinationAdapter = this.BaseModel.Adapters.First(a => a.Configuration.Id == this.BaseModel.Configuration.DestinationAdapterId);

            this.SyncSourceAdapter = SyncTargetViewModelFactory.FromAdapter(sourceAdapter);
            this.SyncDestinationAdapter = SyncTargetViewModelFactory.FromAdapter(destinationAdapter);

            foreach (SyncJob job in this.BaseModel.GetSyncJobHistory())
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    this.SyncJobHistory.Insert(0, new SyncJobViewModel(job, this, true));
                });
            }
        }

        public override void SaveContext()
        {
            throw new NotImplementedException();
        }

        public async Task SaveAsync(bool initializeAfterSave)
        {
            await this.BaseModel.SaveAsync().ConfigureAwait(false);

            if (initializeAfterSave)
            {
                await this.BaseModel.InitializeAsync().ConfigureAwait(false);
            }

            if (!Global.SyncRelationships.Contains(this.BaseModel))
            {
                Global.SyncRelationships.Add(this.BaseModel);
            }
        }

        #endregion

        public TAdapter CreateAdapterViewModel<TAdapter>()
        {
            return (TAdapter)SyncTargetViewModelFactory.CreateFromViewModelType<TAdapter>(this.BaseModel);
        }

        public async Task CalculateRelationshipMetadataAsync()
        {
            this.NextSyncDisplayString = "Calculating...";
            this.RelationshipSizeDisplayString = "Calculating...";
            this.DatabaseSizeDisplayString = "Calculating...";

            Stopwatch stopwatch = Stopwatch.StartNew();

            if (this.TriggerType == SyncTriggerType.Manual)
            {
                this.NextSyncDisplayString = "When manually triggered";
            }
            else if (this.SyncSourceAdapter.AdapterBase.SupportsChangeNotification())
            {
                IChangeNotification changeNotification =
                    (IChangeNotification) this.SyncSourceAdapter.AdapterBase;

                DateTime nextNotify = changeNotification.GetNextNotificationTime();

                if (nextNotify != DateTime.MinValue)
                {
                    this.NextSyncDisplayString = nextNotify.ToString("dddd, MMMM dd, HH:mm:ss");
                }
            }
            else
            {
                this.NextSyncDisplayString = "Unknown!";
            }

            int fileCount = 0;
            int directoryCount = 0;
            long byteCount = 0;

            using (var db = await this.BaseModel.GetDatabaseAsync())
            {
                foreach (SyncEntry syncEntry in db.Entries)
                {
                    if (syncEntry.Type == SyncEntryType.File)
                    {
                        fileCount++;
                        byteCount += syncEntry.GetSize(this.BaseModel, SyncEntryPropertyLocation.Source);
                    }
                    else if (syncEntry.Type == SyncEntryType.Directory)
                    {
                        directoryCount++;
                    }
                }
            }

            this.RelationshipSizeDisplayString = string.Format(
                "{0} in {1} files, {2} folders",
                FileSizeConverter.Convert(byteCount, 2),
                fileCount,
                directoryCount);

            string databasePath = SyncDatabase.GetDatabaseFilePath(this.BaseModel.Configuration.RelationshipId);
            System.IO.FileInfo fileInfo = new System.IO.FileInfo(databasePath);

            this.DatabaseSizeDisplayString = FileSizeConverter.Convert(fileInfo.Length, 2);

            stopwatch.Stop();
            Logger.Debug("Finished CalculateRelationshipMetadataAsync. Duration=" + stopwatch.Elapsed);
        }
    }
}