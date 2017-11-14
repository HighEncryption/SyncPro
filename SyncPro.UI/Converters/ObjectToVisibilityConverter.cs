namespace SyncPro.UI.Converters
{
    using System;
    using System.Globalization;
    using System.Windows;
    using System.Windows.Data;

    [ValueConversion(typeof(object), typeof(Visibility))]
    public class ObjectToVisibilityConverter : IValueConverter
    {
        public bool ReverseValue { get; set; }
        public bool CollapsedWhenFalse { get; set; }

        public ObjectToVisibilityConverter()
        {
            this.ReverseValue = false;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool boolValue = value != null;

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
            return Binding.DoNothing;
        }
    }
}