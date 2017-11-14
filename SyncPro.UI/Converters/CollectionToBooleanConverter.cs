namespace SyncPro.UI.Converters
{
    using System;
    using System.Collections;
    using System.Globalization;
    using System.Windows;
    using System.Windows.Data;

    [ValueConversion(typeof(ICollection), typeof(Visibility))]
    public class CollectionToBooleanConverter : IValueConverter
    {
        public bool ReverseValue { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            ICollection collection = value as ICollection;

            bool result = collection != null && collection.Count > 0;

            if (this.ReverseValue)
            {
                result = !result;
            }

            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}