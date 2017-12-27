namespace SyncPro.UI.Converters
{
    using System;
    using System.Globalization;
    using System.Windows;
    using System.Windows.Data;

    [ValueConversion(typeof(string), typeof(bool))]
    public class StringToBooleanConverter : IValueConverter
    {
        public bool ReverseValue { get; set; }

        public StringToBooleanConverter()
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

            bool isEmpty = string.IsNullOrEmpty(stringValue);

            return this.ReverseValue ? isEmpty : !isEmpty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    [ValueConversion(typeof(string), typeof(bool))]
    public class EnumStringToBooleanConverter : IValueConverter
    {
        public bool ReverseValue { get; set; }

        public EnumStringToBooleanConverter()
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

            bool isMatch = string.Equals(stringValue, parameter);

            return this.ReverseValue ? !isMatch : isMatch;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value != null && (bool)value)
            {
                return parameter as string;
            }

            return null;
        }
    }
}