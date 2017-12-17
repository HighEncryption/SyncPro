namespace SyncPro.UI.RelationshipEditor
{
    using System.Diagnostics;

    using Newtonsoft.Json;

    using SyncPro.Data;
    using SyncPro.Tracing;
    using SyncPro.UI.ViewModels;

    public class SyncTriggeringPageViewModel : WizardPageViewModelBase
    {
        public SyncTriggeringPageViewModel(RelationshipEditorViewModel editorViewModel)
            : base(editorViewModel)
        {
            this.SelectedTriggering = SyncTriggerType.Continuous;
            this.WeeklyDaysOfWeekSelection = new DaysOfWeekSelection();

            // Set default values that will be used when nothing is loaded from context
            this.SelectedScheduleInterval = SyncScheduleInterval.Hourly;
            this.HourlyIntervalValue = 1;
        }

        public override string TabItemImageSource => "/SyncPro.UI;component/Resources/Graphics/clock_20.png";

        public override string PageSubText => "Select when files and folders should be synchronized.";

        public override void LoadContext()
        {
            this.SelectedTriggering = this.EditorViewModel.Relationship.TriggerType;

            // TODO: Fix
            //this.SelectedTriggering = this.EditorViewModel.SyncRelationship.Triggering;
            //this.SelectedScheduleInterval = this.EditorViewModel.SyncRelationship.ScheduleInterval;
            //this.HourlyIntervalValue = this.EditorViewModel.SyncRelationship.HourlyIntervalValue;
            //this.HourlyMinutesPastSyncTime = this.EditorViewModel.SyncRelationship.HourlyMinutesPastSyncTime;
        }

        public override void SaveContext()
        {
            this.EditorViewModel.Relationship.TriggerType = this.SelectedTriggering;
           

            // TODO: FIX ME
            //this.EditorViewModel.Relationship.Model.Configuration.SyncTriggerType = this.SelectedTriggering;
            //this.EditorViewModel.Relationship.Model.Configuration.TriggerConfiguration = this.CreateTriggerConfiguration();
        }

        private string CreateTriggerConfiguration()
        {
            SyncTriggerConfiguration config = new SyncTriggerConfiguration();

            if (this.SelectedTriggering == SyncTriggerType.Scheduled)
            {
                config.ScheduleInterval = this.SelectedScheduleInterval;
                config.HourlyIntervalValue = this.HourlyIntervalValue;
                config.HourlyMinutesPastSyncTime = this.HourlyMinutesPastSyncTime;
            }

            return JsonConvert.SerializeObject(config);
        }

        public override string NavTitle => "Schedule";

        public override string PageTitle => "When to sync";

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private SyncTriggerType selectedTriggering;

        public SyncTriggerType SelectedTriggering
        {
            get { return this.selectedTriggering; }
            set
            {
                if (this.SetProperty(ref this.selectedTriggering, value))
                {
                    switch (value)
                    {
                        case SyncTriggerType.Continuous:
                            this.SelectedTriggeringMessage =
                                "Files and folders will be synchronized whenever a new file or folder is created, or when an existing file is updated or deleted.";
                            break;
                        case SyncTriggerType.Scheduled:
                            this.SelectedTriggeringMessage = "Files and folders will be synchronized at a scheduled time. The next three sync times are shown as the bottom.";
                            break;
                        case SyncTriggerType.Manual:
                            this.SelectedTriggeringMessage = "Files and folders will only be synchronized when manually triggered.";
                            break;
                        case SyncTriggerType.DeviceInsertion:
                            this.SelectedTriggeringMessage = "Files and folders will only be synchronized when a device (such as a drive) is plugged in.";
                            break;
                        default:
                            Logger.Warning("SelectedTriggering value of {0} is invalid. Setting to Manual.", value);
                            this.SelectedTriggering = SyncTriggerType.Manual;
                            break;
                    }
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string selectedTriggeringMessage;

        public string SelectedTriggeringMessage
        {
            get { return this.selectedTriggeringMessage; }
            set { this.SetProperty(ref this.selectedTriggeringMessage, value); }
        }

        #region Scheduled Triggering Properties

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private SyncScheduleInterval selectedScheduleInterval;

        public SyncScheduleInterval SelectedScheduleInterval
        {
            get { return this.selectedScheduleInterval; }
            set { this.SetProperty(ref this.selectedScheduleInterval, value); }
        }

        #region Hourly Properties

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int hourlyIntervalValue;

        public int HourlyIntervalValue
        {
            get { return this.hourlyIntervalValue; }
            set { this.SetProperty(ref this.hourlyIntervalValue, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int hourlyMinutesPastSyncTime;

        public int HourlyMinutesPastSyncTime
        {
            get { return this.hourlyMinutesPastSyncTime; }
            set { this.SetProperty(ref this.hourlyMinutesPastSyncTime, value); }
        }

        #endregion

        #region Weekly Properties

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int weeklyIntervalValue;

        public int WeeklyIntervalValue
        {
            get { return this.weeklyIntervalValue; }
            set { this.SetProperty(ref this.weeklyIntervalValue, value); }
        }

        public DaysOfWeekSelection WeeklyDaysOfWeekSelection { get; private set; }

        #endregion

        #endregion

        #region Event Triggering Properties

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private SyncTriggerType selectedEventTriggering;

        public SyncTriggerType SelectedEventTriggering
        {
            get { return this.selectedEventTriggering; }
            set { this.SetProperty(ref this.selectedEventTriggering, value); }
        }

        #endregion
    }
}