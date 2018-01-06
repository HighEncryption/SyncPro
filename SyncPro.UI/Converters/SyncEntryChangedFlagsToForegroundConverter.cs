namespace SyncPro.UI.Converters
{
    using System;
    using System.Globalization;
    using System.Windows;
    using System.Windows.Data;
    using System.Windows.Media;

    using SyncPro.Adapters;

    [ValueConversion(typeof(SyncEntryChangedFlags), typeof(Visibility))]
    public class SyncEntryChangedFlagsToForegroundConverter : IValueConverter
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
                return Brushes.Black;
            }

            SyncEntryChangedFlags flag = (SyncEntryChangedFlags) value;

            switch (this.Mode)
            {
                case FlagToVisibilityConverterMode.FlagIsSet:
                    if ((flag & desiredFlag) != 0)
                    {
                        return Brushes.Blue;
                    }
                    break;
                case FlagToVisibilityConverterMode.ExactMatch:
                    if (flag == desiredFlag)
                    {
                        return Brushes.Blue;
                    }
                    break;
            }

            return Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}