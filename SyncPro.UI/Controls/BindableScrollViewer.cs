namespace SyncPro.UI.Controls
{
    using System.Windows;
    using System.Windows.Controls;

    public class BindableScrollViewer : ScrollViewer
    {
        public static readonly DependencyProperty BindableHorizontalOffsetProperty = DependencyProperty.Register(
            "BindableHorizontalOffset", typeof (double), typeof (BindableScrollViewer), new PropertyMetadata(default(double), BindableHorizontalOffsetChanged));

        private static void BindableHorizontalOffsetChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs dependencyPropertyChangedEventArgs)
        {
            BindableScrollViewer scrollViewer = dependencyObject as BindableScrollViewer;
            scrollViewer?.ScrollToHorizontalOffset((double)dependencyPropertyChangedEventArgs.NewValue);
        }

        public double BindableHorizontalOffset
        {
            get { return (double) this.GetValue(BindableScrollViewer.BindableHorizontalOffsetProperty); }
            set { this.SetValue(BindableScrollViewer.BindableHorizontalOffsetProperty, value); }
        }
    }
}