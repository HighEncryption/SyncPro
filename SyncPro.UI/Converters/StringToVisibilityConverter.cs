namespace SyncPro.UI.Converters
{
    using System;
    using System.Globalization;
    using System.Windows;
    using System.Windows.Data;

    [ValueConversion(typeof(string), typeof(Visibility))]
    public class StringToVisibilityConverter : IValueConverter
    {
        public bool ReverseValue { get; set; }
        public bool CollapsedWhenFalse { get; set; }

        public StringToVisibilityConverter()
        {
            this.ReverseValue = false;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is string) && value != null)
            {
                return DependencyProperty.UnsetValue;
            }

            string stringValue = (string)value;

            if (this.ReverseValue)
            {
                if (!string.IsNullOrEmpty(stringValue))
                {
                    return this.CollapsedWhenFalse ? Visibility.Collapsed : Visibility.Hidden;
                }

                return Visibility.Visible;
            }

            if (!string.IsNullOrEmpty(stringValue))
            {
                return Visibility.Visible;
            }

            return this.CollapsedWhenFalse ? Visibility.Collapsed : Visibility.Hidden;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}