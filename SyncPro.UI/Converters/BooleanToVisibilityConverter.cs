namespace SyncPro.UI.Converters
{
    using System;
    using System.Globalization;
    using System.Windows;
    using System.Windows.Data;

    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public bool ReverseValue { get; set; }
        public bool CollapsedWhenFalse { get; set; }

        public BooleanToVisibilityConverter()
        {
            this.ReverseValue = false;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is bool))
            {
                return DependencyProperty.UnsetValue;
            }

            bool boolValue = (bool)value;

            if (this.ReverseValue)
            {
                if (boolValue)
                {
                    return this.CollapsedWhenFalse ? Visibility.Collapsed : Visibility.Hidden;
                }

                return Visibility.Visible;
            }

            if (boolValue)
            {
                return Visibility.Visible;
            }

            return this.CollapsedWhenFalse ? Visibility.Collapsed : Visibility.Hidden;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Visibility visibility = (Visibility)value;

            if (visibility == Visibility.Visible)
            {
                return !this.ReverseValue;
            }

            return this.ReverseValue;
        }
    }
}