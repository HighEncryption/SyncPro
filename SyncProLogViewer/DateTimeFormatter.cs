namespace SyncProLogViewer
{
    using System;
    using System.Globalization;
    using System.Windows;
    using System.Windows.Data;

    public class DateTimeFormatter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is DateTime))
            {
                return DependencyProperty.UnsetValue;
            }

            DateTime dt = (DateTime)value;

            string format = parameter as string;

            if (format == null)
            {
                return DependencyProperty.UnsetValue;
            }

            return dt.ToString(format);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}