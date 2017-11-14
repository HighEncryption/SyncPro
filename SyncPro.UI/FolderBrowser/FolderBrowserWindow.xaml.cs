namespace SyncPro.UI.FolderBrowser
{
    using System.Windows.Input;

    using SyncPro.UI.Framework;

    /// <summary>
    /// Interaction logic for FolderBrowserWindow.xaml
    /// </summary>
    public partial class FolderBrowserWindow
    {
        private readonly WindowResizer windowResizer;

        public FolderBrowserWindow()
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
