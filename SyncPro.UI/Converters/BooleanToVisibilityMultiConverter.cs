namespace SyncPro.UI.Converters
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Windows;
    using System.Windows.Data;

    public class BooleanToVisibilityMultiConverter : IMultiValueConverter
    {
        public bool CollapsedWhenFalse { get; set; }

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            Visibility visibility = values.OfType<bool>().All(b => b)
                ? Visibility.Visible
                : (this.CollapsedWhenFalse ? Visibility.Collapsed : Visibility.Hidden);

            return visibility;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}