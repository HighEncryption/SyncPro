namespace SyncPro.UI.Converters
{
    using System;
    using System.Globalization;
    using System.Windows;
    using System.Windows.Data;

    using SyncPro.Runtime;

    [ValueConversion(typeof(SyncJobStage), typeof(Visibility))]
    public class SyncJobStageToVisiblityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string stageName = parameter?.ToString();

            if (string.IsNullOrWhiteSpace(stageName))
            {
                return DependencyProperty.UnsetValue;
            }

            SyncJobStage desiredStage;
            if (!Enum.TryParse(stageName, out desiredStage))
            {
                return DependencyProperty.UnsetValue;
            }

            if (value == null)
            {
                return Visibility.Collapsed;
            }

            SyncJobStage stage = (SyncJobStage) value;

            if (stage == desiredStage)
            {
                return Visibility.Visible;
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}