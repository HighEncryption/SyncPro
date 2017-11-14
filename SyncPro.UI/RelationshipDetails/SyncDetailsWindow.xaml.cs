namespace SyncPro.UI.RelationshipDetails
{
    using System.Windows.Input;

    using SyncPro.UI.Framework;

    /// <summary>
    /// Interaction logic for SyncDetailsWindow.xaml
    /// </summary>
    public partial class SyncDetailsWindow
    {
        private readonly WindowResizer windowResizer;

        public SyncDetailsWindow()
        {
            this.InitializeComponent();
            this.windowResizer = new WindowResizer(this);
        }

        public void Resize(object sender, MouseButtonEventArgs e)
        {
            this.windowResizer.ResizeWindow(sender);
        }

        public void DisplayResizeCursor(object sender, MouseEventArgs e)
        {
            this.windowResizer.DisplayResizeCursor(sender);
        }

        public void ResetCursor(object sender, MouseEventArgs e)
        {
            this.windowResizer.ResetCursor();
        }
    }
}
