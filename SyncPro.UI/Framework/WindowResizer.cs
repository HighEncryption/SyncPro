namespace SyncPro.UI.Framework
{
    using System;
    using System.Windows;
    using System.Windows.Input;
    using System.Windows.Interop;
    using System.Windows.Media;
    using System.Windows.Shapes;

    public class WindowResizer
    {
        private readonly Window activeWindow;

        private HwndSource hwndSource;

        public WindowResizer(Window activeWindow)
        {
            this.activeWindow = activeWindow;

            this.activeWindow.SourceInitialized += this.InitializeWindowSource;
        }

        public void ResetCursor()
        {
            if (Mouse.LeftButton != MouseButtonState.Pressed)
            {
                this.activeWindow.Cursor = Cursors.Arrow;
            }
        }

        public void DragWindow()
        {
            this.activeWindow.DragMove();
        }

        private void InitializeWindowSource(object sender, EventArgs e)
        {
            this.hwndSource = PresentationSource.FromVisual((Visual)sender) as HwndSource;

            Pre.Assert(this.hwndSource != null, "this.hwndSource != null");
        }

        private enum ResizeDirection
        {
            Left = 1,
            Right = 2,
            Top = 3,
            TopLeft = 4,
            TopRight = 5,
            Bottom = 6,
            BottomLeft = 7,
            BottomRight = 8,
        }

        private void InternalResizeWindow(ResizeDirection direction)
        {
            NativeMethods.User32.SendMessage(this.hwndSource.Handle, NativeMethods.User32.WM_SYSCOMMAND, (IntPtr)(61440 + direction), IntPtr.Zero);
        }

        public void ResizeWindow(object sender)
        {
            Rectangle clickedRectangle = sender as Rectangle;

            Pre.Assert(clickedRectangle != null, "clickedRectangle != null");

            switch (clickedRectangle.Name)
            {
                case "PART_ResizeTop":
                    this.activeWindow.Cursor = Cursors.SizeNS;
                    this.InternalResizeWindow(ResizeDirection.Top);
                    break;
                case "PART_ResizeBottom":
                    this.activeWindow.Cursor = Cursors.SizeNS;
                    this.InternalResizeWindow(ResizeDirection.Bottom);
                    break;
                case "PART_ResizeLeft":
                    this.activeWindow.Cursor = Cursors.SizeWE;
                    this.InternalResizeWindow(ResizeDirection.Left);
                    break;
                case "PART_ResizeRight":
                    this.activeWindow.Cursor = Cursors.SizeWE;
                    this.InternalResizeWindow(ResizeDirection.Right);
                    break;
                case "PART_ResizeTopLeft":
                    this.activeWindow.Cursor = Cursors.SizeNWSE;
                    this.InternalResizeWindow(ResizeDirection.TopLeft);
                    break;
                case "PART_ResizeTopRight":
                    this.activeWindow.Cursor = Cursors.SizeNESW;
                    this.InternalResizeWindow(ResizeDirection.TopRight);
                    break;
                case "PART_ResizeBottomLeft":
                    this.activeWindow.Cursor = Cursors.SizeNESW;
                    this.InternalResizeWindow(ResizeDirection.BottomLeft);
                    break;
                case "PART_ResizeBottomRight":
                    this.activeWindow.Cursor = Cursors.SizeNWSE;
                    this.InternalResizeWindow(ResizeDirection.BottomRight);
                    break;
            }
        }

        public void DisplayResizeCursor(object sender)
        {
            Rectangle clickedRectangle = sender as Rectangle;

            Pre.Assert(clickedRectangle != null, "clickedRectangle != null");

            switch (clickedRectangle.Name)
            {
                case "PART_ResizeTop":
                    this.activeWindow.Cursor = Cursors.SizeNS;
                    break;
                case "PART_ResizeBottom":
                    this.activeWindow.Cursor = Cursors.SizeNS;
                    break;
                case "PART_ResizeLeft":
                    this.activeWindow.Cursor = Cursors.SizeWE;
                    break;
                case "PART_ResizeRight":
                    this.activeWindow.Cursor = Cursors.SizeWE;
                    break;
                case "PART_ResizeTopLeft":
                    this.activeWindow.Cursor = Cursors.SizeNWSE;
                    break;
                case "PART_ResizeTopRight":
                    this.activeWindow.Cursor = Cursors.SizeNESW;
                    break;
                case "PART_ResizeBottomLeft":
                    this.activeWindow.Cursor = Cursors.SizeNESW;
                    break;
                case "PART_ResizeBottomRight":
                    this.activeWindow.Cursor = Cursors.SizeNWSE;
                    break;
            }
        }
    }
}