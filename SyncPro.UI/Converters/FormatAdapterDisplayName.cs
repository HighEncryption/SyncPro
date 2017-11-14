namespace SyncPro.UI.Converters
{
    using System;
    using System.Globalization;
    using System.Windows;
    using System.Windows.Data;

    using SyncPro.UI.ViewModels.Adapters;

    public class FormatAdapterDisplayName : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            ISyncTargetViewModel target = value as ISyncTargetViewModel;
            if (target == null)
            {
                return DependencyProperty.UnsetValue;
            }

            return string.Format("{0}  ({1})", target.DestinationPath, target.DisplayName);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}