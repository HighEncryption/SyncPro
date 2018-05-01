namespace SyncPro.UI.RelationshipEditor
{
    using System;
    using System.Diagnostics;

    using SyncPro.Tracing;
    using SyncPro.UI.ViewModels;

    public class SyncTriggeringPageViewModel : WizardPageViewModelBase
    {
        public SyncTriggeringPageViewModel(RelationshipEditorViewModel editorViewModel)
            : base(editorViewModel)
        {
            // Set default values that will be used when nothing is loaded from context
            this.SelectedTriggering = SyncTriggerType.Manual;
            this.SelectedScheduleInterval = TriggerScheduleInterval.Hourly;

            this.HourlyIntervalValue = 1;
            this.HourlyMinutesPastSyncTime = 0;

            this.DailyIntervalValue = 1;
            this.DailyStartTime = DateTime.Now.Date;

            this.WeeklyIntervalValue = 1;
            this.WeeklyStartTime = DateTime.Now.Date;
            this.WeeklyDaysOfWeekSelection = new DaysOfWeekSelection();
        }

        public override string TabItemImageSource => "/SyncPro.UI;component/Resources/Graphics/clock_20.png";

        public override string PageSubText => "Select when files and folders should be synchronized.";

        public override void LoadContext()
        {
            this.SelectedTriggering = this.EditorViewModel.Relationship.TriggerType;

            if (this.SelectedTriggering != SyncTriggerType.Scheduled)
            {
                return;
            }

            this.SelectedScheduleInterval = this.EditorViewModel.Relationship.TriggerScheduleInterval;

            if (this.SelectedScheduleInterval == TriggerScheduleInterval.Hourly)
            {
                this.HourlyIntervalValue = this.EditorViewModel.Relationship.TriggerHourlyInterval;
                this.HourlyMinutesPastSyncTime = this.EditorViewModel.Relationship.TriggerHourlyMinutesPastSyncTime;
            }

            if (this.SelectedScheduleInterval == TriggerScheduleInterval.Daily)
            {
                this.DailyIntervalValue = this.EditorViewModel.Relationship.TriggerDailyIntervalValue;
                this.DailyStartTime = DateTime.Now.Date.Add(
                    this.EditorViewModel.Relationship.TriggerDailyStartTime);
            }

            if (this.SelectedScheduleInterval == TriggerScheduleInterval.Weekly)
            {
                this.WeeklyIntervalValue = this.EditorViewModel.Relationship.TriggerWeeklyIntervalValue;
                this.WeeklyStartTime = DateTime.Now.Date.Add(
                    this.EditorViewModel.Relationship.TriggerWeeklyStartTime);

                this.WeeklyDaysOfWeekSelection.SetFromFlags(
                    this.EditorViewModel.Relationship.TriggerWeeklyDays);
            }
        }

        public override void SaveContext()
        {
            this.EditorViewModel.Relationship.TriggerType = this.SelectedTriggering;
            this.EditorViewModel.Relationship.TriggerScheduleInterval = this.SelectedScheduleInterval;

            this.EditorViewModel.Relationship.TriggerHourlyInterval = this.HourlyIntervalValue;
            this.EditorViewModel.Relationship.TriggerHourlyMinutesPastSyncTime = this.HourlyMinutesPastSyncTime;

            this.EditorViewModel.Relationship.TriggerDailyIntervalValue = this.DailyIntervalValue;
            this.EditorViewModel.Relationship.TriggerDailyStartTime = DailyStartTime?.TimeOfDay ?? TimeSpan.Zero;

            this.EditorViewModel.Relationship.TriggerWeeklyIntervalValue = this.WeeklyIntervalValue;
            this.EditorViewModel.Relationship.TriggerWeeklyStartTime = this.WeeklyStartTime?.TimeOfDay ?? TimeSpan.Zero;
            this.EditorViewModel.Relationship.TriggerWeeklyDays = this.WeeklyDaysOfWeekSelection.ToFlags();
        }

        public override string NavTitle => "Schedule";

        public override string PageTitle => "When to sync";

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private SyncTriggerType selectedTriggering;

        public SyncTriggerType SelectedTriggering
        {
            get => this.selectedTriggering;
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
            get => this.selectedTriggeringMessage;
            set => this.SetProperty(ref this.selectedTriggeringMessage, value);
        }

        #region Scheduled Triggering Properties

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private TriggerScheduleInterval selectedScheduleInterval;

        public TriggerScheduleInterval SelectedScheduleInterval
        {
            get => this.selectedScheduleInterval;
            set => this.SetProperty(ref this.selectedScheduleInterval, value);
        }

        #region Hourly Properties

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int hourlyIntervalValue;

        public int HourlyIntervalValue
        {
            get => this.hourlyIntervalValue;
            set => this.SetProperty(ref this.hourlyIntervalValue, value);
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int hourlyMinutesPastSyncTime;

        public int HourlyMinutesPastSyncTime
        {
            get => this.hourlyMinutesPastSyncTime;
            set => this.SetProperty(ref this.hourlyMinutesPastSyncTime, value);
        }

        #endregion

        #region Daily Properties

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int dailyIntervalValue;

        public int DailyIntervalValue
        {
            get => this.dailyIntervalValue;
            set => this.SetProperty(ref this.dailyIntervalValue, value);
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private DateTime? dailyStartTime;

        public DateTime? DailyStartTime
        {
            get => this.dailyStartTime;
            set => this.SetProperty(ref this.dailyStartTime, value);
        }

        #endregion

        #region Weekly Properties

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int weeklyIntervalValue;

        public int WeeklyIntervalValue
        {
            get => this.weeklyIntervalValue;
            set => this.SetProperty(ref this.weeklyIntervalValue, value);
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private DateTime? weeklyStartTime;

        public DateTime? WeeklyStartTime
        {
            get => this.weeklyStartTime;
            set => this.SetProperty(ref this.weeklyStartTime, value);
        }

        public DaysOfWeekSelection WeeklyDaysOfWeekSelection { get; }

        #endregion

        #endregion

        #region Event Triggering Properties

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private SyncTriggerType selectedEventTriggering;

        public SyncTriggerType SelectedEventTriggering
        {
            get => this.selectedEventTriggering;
            set => this.SetProperty(ref this.selectedEventTriggering, value);
        }

        #endregion
    }
}