namespace OneDriveTokenHelper
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Windows.Threading;

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        private Dispatcher localDispatcher;

        internal new static App Current { get; private set; }

        internal static int Start(Dictionary<string, string> args)
        {
            App app = new App();
            Current = app;

            app.localDispatcher = Dispatcher.CurrentDispatcher;

            using (Task task = new Task(app.Initialize, args))
            {
                task.Start();
                app.Run();
            }

            return TokenProvider.TokenSuccess ? 1 : 0;
        }

        private void Initialize(object objArgs)
        {
            Dictionary<string, string> args = (Dictionary<string, string>) objArgs;
            if (args.ContainsKey("getToken"))
            {
                this.localDispatcher.Invoke(() =>
                {
                    TokenProvider.SignIn(args["path"]);
                });
            }
        }
    }
}
