namespace SyncPro.UI.Converters
{
    using System;
    using System.Globalization;
    using System.Windows;
    using System.Windows.Data;

    using SyncPro.Adapters;
    using SyncPro.Runtime;

    public enum FlagToVisibilityConverterMode
    {
        FlagIsSet,
        ExactMatch
    }

    [ValueConversion(typeof(SyncJobStage), typeof(Visibility))]
    public class SyncEntryChangedFlagsToVisibilityConverter : IValueConverter
    {
        public FlagToVisibilityConverterMode Mode { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string enumValue = parameter?.ToString();

            if (string.IsNullOrWhiteSpace(enumValue))
            {
                return DependencyProperty.UnsetValue;
            }

            SyncEntryChangedFlags desiredFlag;
            if (!Enum.TryParse(enumValue, out desiredFlag))
            {
                return DependencyProperty.UnsetValue;
            }

            if (value == null)
            {
                return Visibility.Collapsed;
            }

            SyncEntryChangedFlags flag = (SyncEntryChangedFlags) value;

            switch (this.Mode)
            {
                case FlagToVisibilityConverterMode.FlagIsSet:
                    if ((flag & desiredFlag) != 0)
                    {
                        return Visibility.Visible;
                    }
                    break;
                case FlagToVisibilityConverterMode.ExactMatch:
                    if (flag == desiredFlag)
                    {
                        return Visibility.Visible;
                    }
                    break;
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}