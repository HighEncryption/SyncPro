namespace SyncPro.UI.Converters
{
    using System;
    using System.Collections;
    using System.Globalization;
    using System.Windows;
    using System.Windows.Data;

    [ValueConversion(typeof(ICollection), typeof(Visibility))]
    public class CollectionToVisibilityConverter : IValueConverter
    {
        public bool EmptyIsCollapsed { get; set; }

        public bool ReverseValue { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            ICollection collection = value as ICollection;

            bool result = collection != null && collection.Count > 0;

            if (this.ReverseValue)
            {
                result = !result;
            }

            if (result)
            {
                return Visibility.Visible;
            }

            return this.EmptyIsCollapsed ? Visibility.Collapsed : Visibility.Hidden;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}