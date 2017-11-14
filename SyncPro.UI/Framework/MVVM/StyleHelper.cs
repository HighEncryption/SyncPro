namespace SyncPro.UI.Framework.MVVM
{
    using System.Diagnostics.CodeAnalysis;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Windows.Shapes;

    internal static class StyleHelper
    {
        private const int maxParentSearchDepth = 8;

        [SuppressMessage("Microsoft.Design", "CA1011:ConsiderPassingBaseTypesAsParameters")]
        public static void ApplyChromelessWindowStyle(Window window)
        {
            // Attach the window drag handler
            Grid headerGrid = window.Template.FindName("PART_HeaderGrid", window) as Grid;

            if (headerGrid != null)
            {
                headerGrid.MouseLeftButtonDown += HeaderGridMouseLeftButtonDown;
            }

            // Attach the window minimize button handler
            Button minimizeButton = window.Template.FindName("PART_MinimizeWindowButton", window) as Button;

            if (minimizeButton != null)
            {
                minimizeButton.Click += MinimizeButtonOnClick;
            }

            IResizableWindow resizableWindow = window as IResizableWindow;

            if (resizableWindow != null)
            {
                // Attach the window resize event handlers
                // TODO: Should we look at ResizeMode to determine whether or not to hide these?
                string[] resizeAreaNames = { "TopLeft", "Top", "TopRight", "Left", "Right", "BottomLeft", "Bottom", "BottomRight" };

                foreach (string areaName in resizeAreaNames)
                {
                    Rectangle resizeRectangle = window.Template.FindName("PART_Resize" + areaName, window) as Rectangle;

                    Pre.Assert(resizeRectangle != null, "resizeRectangle != null");

                    resizeRectangle.MouseEnter += resizableWindow.DisplayResizeCursor;
                    resizeRectangle.MouseLeave += resizableWindow.ResetCursor;
                    resizeRectangle.PreviewMouseDown += resizableWindow.Resize;
                }
            }
        }

        private static void MinimizeButtonOnClick(object sender, RoutedEventArgs routedEventArgs)
        {
            Button button = sender as Button;

            if (button == null)
            {
                return;
            }

            Window window = FindParentWindow(button);

            if (window != null)
            {
                window.WindowState = WindowState.Minimized;
            }
        }

        private static void HeaderGridMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Grid senderGrid = sender as Grid;

            if (senderGrid == null)
            {
                return;
            }

            Window window = FindParentWindow(senderGrid);

            window?.DragMove();
        }

        private static Window FindParentWindow(DependencyObject dependencyObject)
        {
            DependencyObject obj = dependencyObject;

            for (int i = 0; i < maxParentSearchDepth; i++)
            {
                obj = VisualTreeHelper.GetParent(obj);

                if (obj == null)
                {
                    // We hit the top of the visual tree without finding the Window.
                    return null;
                }

                Window window = obj as Window;

                if (window != null)
                {
                    return window;
                }
            }

            return null;
        }
    }
}