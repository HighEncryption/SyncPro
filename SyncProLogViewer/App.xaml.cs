namespace SyncProLogViewer
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using System.Windows.Threading;

    using SyncProLogViewer.ViewModels;

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        private Dispatcher localDispatcher;

        internal new static App Current { get; private set; }

        public string ConfigDirectoryPath { get; private set; }

        public MainWindowViewModel MainWindowsViewModel => this.mainWindowViewModel;

        private MainWindowViewModel mainWindowViewModel;
        internal static void Start()
        {
            App app = new App();
            Current = app;

            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                Exception e = args.ExceptionObject as Exception;
                string message = e?.ToString() ?? "(null)";

                File.WriteAllText(
                    Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        string.Format("syncprologviewer.exception.{0:yyyyMMddHHmmss}.txt", DateTime.Now)),
                    message);
            };

            app.ConfigDirectoryPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                "SyncProLogViewer");

            if (!Directory.Exists(app.ConfigDirectoryPath))
            {
                Directory.CreateDirectory(app.ConfigDirectoryPath);
            }

            app.localDispatcher = Dispatcher.CurrentDispatcher;
            using (app.mainWindowViewModel = new MainWindowViewModel())
            using (Task task = new Task(app.Initialize))
            {
                task.Start();
                app.Run();
            }


        }

        private void Initialize()
        {
            // If there are no sync relationships, show the main window
            this.localDispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(this.ShowMainWindow));
        }

        private void ShowMainWindow()
        {
            if (this.MainWindow == null)
            {
                this.MainWindow = new MainWindow { DataContext = this.mainWindowViewModel };
                this.MainWindow.Show();
            }
        }

        public static void DispatcherInvoke(Action action)
        {
            App.Current.localDispatcher.Invoke(action);
        }
    }
}
