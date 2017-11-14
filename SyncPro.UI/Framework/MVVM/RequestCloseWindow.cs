namespace SyncPro.UI.Framework.MVVM
{
    using System;
    using System.Windows;
    using System.Windows.Input;
    using System.Windows.Threading;

    public class RequestCloseWindow : Window
    {
        protected RequestCloseWindow()
        {
            this.DataContextChanged += this.OnDataContextChanged;

            this.Closing += (sender, args) =>
            {
                IRequestClose requestClose = this.DataContext as IRequestClose;

                if (requestClose != null)
                {
                    requestClose.WindowClosing(args);

                    // Disconnect the event handler in case the view model is reused.
                    requestClose.RequestClose -= this.ViewModelRequestClose;
                }
            };
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            IRequestClose oldViewModel = e.OldValue as IRequestClose;

            if (oldViewModel != null)
            {
                oldViewModel.RequestClose -= this.ViewModelRequestClose;
            }

            IRequestClose newViewModel = e.NewValue as IRequestClose;

            if (newViewModel != null)
            {
                newViewModel.RequestClose += this.ViewModelRequestClose;
            }
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            StyleHelper.ApplyChromelessWindowStyle(this);
        }

        private void ViewModelRequestClose(object sender, RequestCloseEventArgs e)
        {
            this.Dispatcher.BeginInvoke(
                DispatcherPriority.Normal,
                new Action(
                    delegate
                    {
                        if (e.DialogResult != null)
                        {
                            this.DialogResult = e.DialogResult;
                        }

                        this.Close();
                    }));
        }
    }

    public class RequestCloseResizableWindow : RequestCloseWindow, IResizableWindow
    {
        private readonly WindowResizer windowResizer;

        public RequestCloseResizableWindow()
        {
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