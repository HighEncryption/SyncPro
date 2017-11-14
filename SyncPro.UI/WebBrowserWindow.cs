////namespace SyncPro.UI
////{
////    using System;
////    using System.Threading;
////    using System.Windows;
////    using System.Windows.Controls;
////    using System.Windows.Interop;
////    using System.Windows.Navigation;

////    public class WebBrowserWindow
////    {
////        //private Application application;
////        private Window window;

////        public void Show(string uriString)
////        {
////            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
////            {
////                throw new InvalidOperationException("STAThread is required on the entry point");
////            }

////            //this.application = new Application();
////            this.window = new Window();

////            WebBrowser webBrowser = new WebBrowser();
////            this.window.Content = webBrowser;

////            webBrowser.Loaded += (sender, eventArgs) =>
////            {
////                webBrowser.Navigate(uriString);
////                NativeMethods.SetForegroundWindow(new WindowInteropHelper(this.window).Handle);
////                this.window.Width = 500;
////                this.window.Height = 900;
////            };

////            webBrowser.Navigating += (sender, eventArgs) =>
////            {
////                this.Navigating?.Invoke(this, eventArgs);
////            };

////            webBrowser.Navigated += (sender, eventArgs) =>
////            {
////                this.Navigated?.Invoke(this, eventArgs);
////            };

////            //this.window.Closed += (sender, eventArgs) => this.application.Shutdown();

////            //this.application.Run(this.window);
////        }

////        public void Close()
////        {
////            this.window.Close();
////        }

////        public event NavigatingCancelEventHandler Navigating;
////        public event NavigatedEventHandler Navigated;
////    }
////}