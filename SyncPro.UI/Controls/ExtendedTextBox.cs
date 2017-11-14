namespace SyncPro.UI.Controls
{
    using System.Windows;
    using System.Windows.Controls;

    public class ExtendedTextBox : TextBox
    {
        public string InlayText
        {
            get { return (string) this.GetValue(InlayTextProperty); }
            set { this.SetValue(InlayTextProperty, value); }
        }

        public static readonly DependencyProperty InlayTextProperty = DependencyProperty.Register(
            "InlayText",
            typeof(string),
            typeof(ExtendedTextBox),
            new PropertyMetadata(null));

        public HorizontalAlignment InlayTextHorizontalAlignment
        {
            get { return (HorizontalAlignment) this.GetValue(InlayTextHorizontalAlignmentProperty); }
            set { this.SetValue(InlayTextHorizontalAlignmentProperty, value); }
        }

        public static readonly DependencyProperty InlayTextHorizontalAlignmentProperty = DependencyProperty.Register(
            "InlayTextHorizontalAlignment",
            typeof(HorizontalAlignment),
            typeof(ExtendedTextBox),
            new PropertyMetadata(null));

        public double InlayTextFontSize
        {
            get { return (double) this.GetValue(InlayTextFontSizeProperty); }
            set { this.SetValue(InlayTextFontSizeProperty, value); }
        }

        public static readonly DependencyProperty InlayTextFontSizeProperty = DependencyProperty.Register(
            "InlayTextFontSize",
            typeof(double),
            typeof(ExtendedTextBox),
            new PropertyMetadata(null));

        public FontWeight InlayTextFontWeight
        {
            get { return (FontWeight) this.GetValue(InlayTextFontWeightProperty); }
            set { this.SetValue(InlayTextFontWeightProperty, value); }
        }

        public static readonly DependencyProperty InlayTextFontWeightProperty = DependencyProperty.Register(
            "InlayTextFontWeight",
            typeof(FontWeight),
            typeof(ExtendedTextBox),
            new PropertyMetadata(null));

        public FontStyle InlayTextFontStyle
        {
            get { return (FontStyle) this.GetValue(InlayTextFontStyleProperty); }
            set { this.SetValue(InlayTextFontStyleProperty, value); }
        }

        public static readonly DependencyProperty InlayTextFontStyleProperty = DependencyProperty.Register(
            "InlayTextFontStyle",
            typeof(FontStyle),
            typeof(ExtendedTextBox),
            new PropertyMetadata(null));


        public string HelpText
        {
            get { return (string) this.GetValue(HelpTextProperty); }
            set { this.SetValue(HelpTextProperty, value); }
        }

        public static readonly DependencyProperty HelpTextProperty = DependencyProperty.Register(
            "HelpText",
            typeof(string),
            typeof(ExtendedTextBox),
            new PropertyMetadata(null));
    }

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
            get { return (double) this.GetValue(BindableHorizontalOffsetProperty); }
            set { this.SetValue(BindableHorizontalOffsetProperty, value); }
        }
    }

}