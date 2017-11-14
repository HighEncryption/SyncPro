namespace SyncPro.UI.RelationshipEditor
{
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Linq;

    using SyncPro.UI.ViewModels;

    public class ThrottlingUnit
    {
        public string DisplayName { get; set; }

        public int ScaleFactor { get; set; }
    }

    public class SyncPerformancePageViewModel : WizardPageViewModelBase
    {
        public SyncPerformancePageViewModel(RelationshipEditorViewModel editorViewModel)
            : base(editorViewModel)
        {
            this.ThrottlingValueText = "1";

            this.ThrottlingUnits = new List<ThrottlingUnit>
            {
                new ThrottlingUnit { ScaleFactor = 0x1, DisplayName = "B/sec" },
                new ThrottlingUnit { ScaleFactor = 0x400, DisplayName = "KB/sec" },
                new ThrottlingUnit { ScaleFactor = 0x100000, DisplayName = "MB/sec" }
            };

            this.SelectedThrottlingUnit = this.ThrottlingUnits.Last();
        }

        public override string TabItemImageSource => "/SyncPro.UI;component/Resources/Graphics/signal_4_20.png";

        public override void LoadContext()
        {
            this.IsThrottlingEnabled = this.EditorViewModel.Relationship.IsThrottlingEnabled;

            if (this.EditorViewModel.Relationship.ThrottlineValue > 0)
            {
                this.ThrottlingValueText = this.EditorViewModel.Relationship.ThrottlineValue.ToString();
            }

            ThrottlingUnit scaleFactorUnit =
                this.ThrottlingUnits.FirstOrDefault(i => i.ScaleFactor == this.EditorViewModel.Relationship.ThrottlingScaleFactor);

            if (scaleFactorUnit != null)
            {
                this.SelectedThrottlingUnit = scaleFactorUnit;
            }
        }

        public override void SaveContext()
        {
            this.EditorViewModel.Relationship.IsThrottlingEnabled = this.IsThrottlingEnabled;
            this.EditorViewModel.Relationship.ThrottlineValue = int.Parse(this.ThrottlingValueText);
            this.EditorViewModel.Relationship.ThrottlingScaleFactor = this.SelectedThrottlingUnit.ScaleFactor;
        }

        public override string NavTitle => "Performance";

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string restrictedStartTime;

        public string RestrictedStartTime
        {
            get { return this.restrictedStartTime; }
            set { this.SetProperty(ref this.restrictedStartTime, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool isThrottlingEnabled;

        public bool IsThrottlingEnabled
        {
            get { return this.isThrottlingEnabled; }
            set { this.SetProperty(ref this.isThrottlingEnabled, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string throttlingValueText;

        public string ThrottlingValueText
        {
            get { return this.throttlingValueText; }
            set { this.SetProperty(ref this.throttlingValueText, value); }
        }

        public List<ThrottlingUnit> ThrottlingUnits { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private ThrottlingUnit selectedThrottlingUnit;

        public ThrottlingUnit SelectedThrottlingUnit
        {
            get { return this.selectedThrottlingUnit; }
            set { this.SetProperty(ref this.selectedThrottlingUnit, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool isThrottlingTimesEnabled;

        public bool IsThrottlingTimesEnabled
        {
            get { return this.isThrottlingTimesEnabled; }
            set { this.SetProperty(ref this.isThrottlingTimesEnabled, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool isDataUsageEnabled;

        public bool IsDataUsageEnabled
        {
            get { return this.isDataUsageEnabled; }
            set { this.SetProperty(ref this.isDataUsageEnabled, value); }
        }
    }
}