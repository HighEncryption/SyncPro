namespace SyncPro.UI.Converters
{
    using System;
    using System.Globalization;
    using System.Numerics;
    using System.Windows;
    using System.Windows.Data;

    public class FileSizeConverter : IValueConverter
    {
        private const ulong Kb = 1024;
        private const ulong Mb = 1024 * 1024;
        private const ulong Gb = 1024 * 1024 * 1024;
        private const ulong Tb = (ulong)1024 * 1024 * 1024 * 1024;

        public string FormatType { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return DependencyProperty.UnsetValue;
            }

            int formatType;
            if (!int.TryParse(this.FormatType, out formatType))
            {
                formatType = 1;
            }

            if (value is BigInteger)
            {
                BigInteger bigInteger = (BigInteger)value;
                return Convert(bigInteger, formatType);
            }

            ulong result;
            if (!UInt64.TryParse(System.Convert.ToString(value), out result))
            {
                return Convert(0, formatType);
            }

            return Convert(result, formatType);
        }

        public static string Convert(ulong val, int formatType)
        {
            if (Kb > val)
            {
                return string.Format(GetFormatString(0, 1), val);
            }

            if (Mb > val && val >= Kb)
            {
                return string.Format(GetFormatString(1, formatType), (double)val / Kb);
            }

            if (Gb > val && val >= Mb)
            {
                return string.Format(GetFormatString(2, formatType), (double)val / Mb);
            }

            if (Tb > val && val >= Gb)
            {
                return string.Format(GetFormatString(3, formatType), (double)val / Gb);
            }

            if (val >= Tb)
            {
                return string.Format(GetFormatString(4, formatType), (double)val / Tb);
            }

            throw new NotImplementedException();
        }

        public static string Convert(BigInteger val, int formatType)
        {
            if (Kb > val)
            {
                return string.Format(GetFormatString(0, formatType), val);
            }

            if (Mb > val && val >= Kb)
            {
                return string.Format(GetFormatString(1, formatType), (double)val / Kb);
            }

            if (Gb > val && val >= Mb)
            {
                return string.Format(GetFormatString(2, formatType), (double)val / Mb);
            }

            if (Tb > val && val >= Gb)
            {
                return string.Format(GetFormatString(3, formatType), (double)val / Gb);
            }

            if (val >= Tb)
            {
                return string.Format(GetFormatString(4, formatType), (double)val / Tb);
            }

            throw new NotImplementedException();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private static string GetFormatString(int magnitude, int type)
        {
            if (magnitude == 0)
            {
                return "{0} B";
            }

            if (type == 1)
            {
                if (magnitude == 1)
                {
                    return "{0:##0.0##} KB";
                }
                if (magnitude == 2)
                {
                    return "{0:##0.0##} MB";
                }
                if (magnitude == 3)
                {
                    return "{0:##0.0##} GB";
                }
                if (magnitude == 4)
                {
                    return "{0:##0.0##} TB";
                }
            }

            if (type == 2)
            {
                if (magnitude == 1)
                {
                    return "{0:##0.0} KB";
                }
                if (magnitude == 2)
                {
                    return "{0:##0.0} MB";
                }
                if (magnitude == 3)
                {
                    return "{0:##0.0} GB";
                }
                if (magnitude == 4)
                {
                    return "{0:##0.0} TB";
                }
            }

            throw new NotImplementedException("Not implemented: Magnitude:" + magnitude + ", Type:" + type);
        }
    }
}