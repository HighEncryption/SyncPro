namespace SyncPro.UI.Converters
{
    using System;
    using System.Globalization;
    using System.Windows;
    using System.Windows.Data;

    public class LevelToIndentConverter : IValueConverter
    {
        public double IndentSize { get; set; }

        public double InitialIndentSize { get; set; }

        public LevelToIndentConverter()
        {
            this.IndentSize = 19.0;
            this.InitialIndentSize = 0;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int)
            {
                return new Thickness((int)this.InitialIndentSize + ((int)value * this.IndentSize), 0, 0, 0);
            }

            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object o, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}