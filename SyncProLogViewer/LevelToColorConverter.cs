namespace SyncProLogViewer
{
    using System;
    using System.Globalization;
    using System.Windows.Data;
    using System.Windows.Media;

    public class LevelToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string str = value as string;

            if (str == "ERROR")
            {
                return Brushes.Red;
            }

            if (str == "WARN")
            {
                return Brushes.DarkOrange;
                //return new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0x00));
            }

            if (str == "VERB" || str == "DEBUG")
            {
                return new SolidColorBrush(Color.FromRgb(0xaa, 0xaa, 0xaa));
            }

            return Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}