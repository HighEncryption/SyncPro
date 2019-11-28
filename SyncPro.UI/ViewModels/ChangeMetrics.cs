namespace SyncPro.UI.ViewModels
{
    using System.Diagnostics.CodeAnalysis;

    using SyncPro.UI.Framework;

    public class ChangeMetrics : NotifyPropertyChangedSlim
    {
        public ChangeMetrics(string displayName, bool displayAsByteSize = false)
        {
            this.DisplayName = displayName;
            this.DisplayAsByteSize = displayAsByteSize;
        }

        public bool DisplayAsByteSize { get; }

        public string DisplayName { get; }

        public long Added { get; set; }

        public long Existing { get; set; }

        public long Modified { get; set; }

        public long Metadata { get; set; }

        public long Removed { get; set; }

        public long Unchanged { get; set; }

        [SuppressMessage("ReSharper", "ExplicitCallerInfoArgument")]
        public void RaisePropertiesChanged()
        {
            this.RaisePropertyChanged(nameof(this.Added));
            this.RaisePropertyChanged(nameof(this.Existing));
            this.RaisePropertyChanged(nameof(this.Modified));
            this.RaisePropertyChanged(nameof(this.Metadata));
            this.RaisePropertyChanged(nameof(this.Removed));
            this.RaisePropertyChanged(nameof(this.Unchanged));
        }
    }
}