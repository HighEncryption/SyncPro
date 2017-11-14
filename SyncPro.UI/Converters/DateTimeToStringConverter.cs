namespace SyncPro.UI.Converters
{
    using System;
    using System.Globalization;
    using System.Windows;
    using System.Windows.Data;

    [ValueConversion(typeof(DateTime), typeof(string))]
    public class DateTimeToStringConverter : IValueConverter
    {
        public string Format { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string format = parameter as string;

            if (string.IsNullOrWhiteSpace(format))
            {
                format = this.Format;
            }

            if (string.IsNullOrWhiteSpace(format))
            {
                throw new FormatException("The format specified by the parameter was empty");
            }

            if (!(value is DateTime))
            {
                return DependencyProperty.UnsetValue;
            }

            DateTime dt = (DateTime)value;
            return dt.ToString(format);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}