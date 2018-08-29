namespace SyncPro.UI
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media.Imaging;
    using System.Windows.Threading;

    using Hardcodet.Wpf.TaskbarNotification;

    using SyncPro.Adapters;
    using SyncPro.Adapters.BackblazeB2;
    using SyncPro.Adapters.GoogleDrive;
    using SyncPro.Adapters.MicrosoftOneDrive;
    using SyncPro.Adapters.WindowsFileSystem;
    using SyncPro.Counters;
    using SyncPro.Data;
    using SyncPro.Runtime;
    using SyncPro.Tracing;
    using SyncPro.UI.Dialogs;
    using SyncPro.UI.Framework.MVVM;
    using SyncPro.UI.Navigation;
    using SyncPro.UI.Navigation.ViewModels;
    using SyncPro.UI.ViewModels;

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        private Dispatcher localDispatcher;

        internal new static App Current { get; private set; }

        public MainWindowViewModel MainWindowsViewModel => 
            this.mainWindowViewModel;

        private MainWindowViewModel mainWindowViewModel;
        private TaskbarIcon notifyIcon;

        //public SettingsFile Settings { get; private set; }
        public string AppDataRoot { get; private set; }

        //private string settingsFilePath;

        public ICommand ShowConfigureWindowCommand { get; }

        public ICommand ShutdownApplicationCommand { get; }

        private bool testMode;

        public bool TestMode
        {
            get { return this.testMode; }
            set
            {
                this.testMode = value;
                this.MainWindowsViewModel.UpdateWindowTitle();
                if (value)
                {
                    LogViewerHelper.LaunchLogViewer(null, false);
                }
            }
        }

        public App()
        {
            this.InitializeComponent();
            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            this.ShowConfigureWindowCommand = new DelegatedCommand(o => this.ShowMainWindow());
            this.ShutdownApplicationCommand = new DelegatedCommand(o => this.Shutdown());
        }

        internal static void Start(Dictionary<string, string> args)
        {
            bool testMode = Keyboard.IsKeyDown(Key.LeftShift);
            Global.Initialize(testMode);
            if (testMode)
            {
                LogViewerHelper.LaunchLogViewer(null, false);
            }

            // Start the counter manager before initializing the adapters in case the initialization
            // causes any counters to be emitted.
            CounterManager.Start();

            CounterManager.LogCounter("AppStart", 1);

            AdapterRegistry.RegisterAdapter(
                BackblazeB2Adapter.TargetTypeId,
                typeof(BackblazeB2Adapter),
                typeof(BackblazeB2AdapterConfiguration));

            AdapterRegistry.RegisterAdapter(
                GoogleDriveAdapter.TargetTypeId,
                typeof(GoogleDriveAdapter),
                typeof(GoogleDriveAdapterConfiguration));

            AdapterRegistry.RegisterAdapter(
                OneDriveAdapter.TargetTypeId,
                typeof(OneDriveAdapter),
                typeof(OneDriveAdapterConfiguration));

            AdapterRegistry.RegisterAdapter(
                WindowsFileSystemAdapter.TargetTypeId,
                typeof(WindowsFileSystemAdapter),
                typeof(WindowsFileSystemAdapterConfiguration));

            // Enable property validation logging if trying to debug validation problems
            // LoggerExtensions.LogPropertyValidation = true;

            App app = new App();
            Current = app;
            Current.testMode = testMode;

            bool runInBackground = args.ContainsKey("runInBackground");

            if (runInBackground)
            {
                // ReSharper disable once UseObjectOrCollectionInitializer
                app.notifyIcon = new TaskbarIcon();
                app.notifyIcon.IconSource =
                    new BitmapImage(new Uri("pack://application:,,,/SyncPro.UI;component/Resources/Graphics/SyncProIcon.ico"));
                app.notifyIcon.ContextMenu = (ContextMenu)app.FindResource("TaskbarIconMenu");
                app.notifyIcon.DoubleClickCommand = app.ShowConfigureWindowCommand;
            }

            app.localDispatcher = Dispatcher.CurrentDispatcher;
            app.mainWindowViewModel = new MainWindowViewModel();

            if (!runInBackground)
            {
                app.mainWindowViewModel.RequestClose += (s, a) =>
                {
                    Application.Current.Shutdown(0);
                };
            }

            // Load settings from disk
            app.LoadSettings();

            if (runInBackground && app.mainWindowViewModel.SyncRelationships.OfType<SyncRelationshipViewModel>().Any())
            {
                app.notifyIcon.ShowBalloonTip(
                    "SyncPro Running",
                    string.Format(
                        "Loaded {0} sync relationships.",
                        app.mainWindowViewModel.SyncRelationships.OfType<SyncRelationshipViewModel>().Count()),
                    BalloonIcon.Info);
            }

            using (Task task = new Task(app.Initialize, runInBackground))
            {
                task.Start();
                app.Run();
            }

            if (runInBackground)
            {
                app.notifyIcon.Visibility = Visibility.Collapsed;
                app.notifyIcon.Dispose();
            }

            CounterManager.Stop();
            Global.SaveAppConfig();
        }

        private void LoadSettings()
        {
            Logger.InitializeApplicationStart();

            string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            this.AppDataRoot = Path.Combine(localAppDataPath, "SyncPro");

            if (!Directory.Exists(this.AppDataRoot))
            {
                Directory.CreateDirectory(this.AppDataRoot);
            }

            DirectoryInfo appDataRootDir = new DirectoryInfo(this.AppDataRoot);

            this.mainWindowViewModel.NavigationItems.Add(new DashboardNodeViewModel(null, new DashboardViewModel()));

            List<SyncRelationshipViewModel> relationships = new List<SyncRelationshipViewModel>();

            foreach (DirectoryInfo relationshipDir in appDataRootDir.GetDirectories())
            {
                Guid guid;
                if (Guid.TryParse(relationshipDir.Name, out guid))
                {
                    SyncRelationship relationship;
                    try
                    {
                        relationship = SyncRelationship.Load(guid);
                    }
                    catch (Exception e)
                    {
                        Logger.Error("Failed to load relationship " + guid.ToString("D") + ": " + e);
                        continue;
                    }

                    Global.SyncRelationships.Add(relationship);

                    SyncDatabase.UpdateIfNeeded(relationship.Configuration.RelationshipId);
                    
                    //relationship.BeginInitialize();

                    // Disable the warning about not awaiting the sync call to InitializeAsync, since we want it to execute
                    // asynchronously while we continue with the view model initialization.
#pragma warning disable 4014
                    relationship.InitializeAsync().ConfigureAwait(false);
#pragma warning restore 4014

                    SyncRelationshipViewModel relationshipViewModel = new SyncRelationshipViewModel(relationship, true);

                    relationships.Add(relationshipViewModel);
                }
            }

            // Create the navigation nodes for each of the relationships
            foreach (var relationship in relationships.OrderBy(r => r.Name))
            {
                this.mainWindowViewModel.SyncRelationships.Add(relationship);
                this.mainWindowViewModel.NavigationItems.Add(new SyncRelationshipNodeViewModel(null, relationship));
            }

            if (this.mainWindowViewModel.NavigationItems.Any())
            {
                this.mainWindowViewModel.NavigationItems.First().IsSelected = true;
            }

            Logger.InitializeApplicationStop();
        }

        private void Initialize(object runInBackground)
        {
            // If there are no sync relationships, show the main window
            if (!this.mainWindowViewModel.SyncRelationships.Any() || !(bool)runInBackground)
            {
                this.localDispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(this.ShowMainWindow));
            }

            this.localDispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(this.ShowFirstRunDialog));
        }

        private void ShowMainWindow()
        {
            if (this.MainWindow == null)
            {
                this.MainWindow = new MainWindow { DataContext = this.mainWindowViewModel };
                this.MainWindow.Show();
            }
        }

        private void ShowFirstRunDialog()
        {
            if (Global.AppConfig.AcceptUsage)
            {
                return;
            }

            FirstRunDialogViewModel viewModel = new FirstRunDialogViewModel();
            var firstRunDialog = new FirstRunDialog()
            {
                DataContext = viewModel
            };

            //Current.localDispatcher.Invoke(() => { firstRunDialog.ShowDialog(); });
            firstRunDialog.ShowDialog();

            if (viewModel.AcceptUsage)
            {
                Global.AppConfig.AcceptUsage = true;
                Global.SaveAppConfig();
            }

            if (!Global.AppConfig.AcceptUsage)
            {
                Application.Current.Shutdown();
            }
        }

        public static void DispatcherInvoke(Action action)
        {
            Current.localDispatcher.Invoke(action);
        }
    }
}
