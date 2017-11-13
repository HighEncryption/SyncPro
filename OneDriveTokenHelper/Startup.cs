namespace OneDriveTokenHelper
{
    using System;
    using System.Collections.Generic;

    public class Startup
    {
        [STAThread]
        internal static int Main(string[] arguments)
        {
            Dictionary<string, string> args = CommandLineHelper.ParseCommandLineArgs(arguments);
            return App.Start(args);
        }
    }
}