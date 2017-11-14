namespace SyncPro.UI.Framework
{
    using System.Windows.Input;

    public interface IResizableWindow
    {
        void Resize(object sender, MouseButtonEventArgs e);

        void DisplayResizeCursor(object sender, MouseEventArgs e);

        void ResetCursor(object sender, MouseEventArgs e);
    }
}