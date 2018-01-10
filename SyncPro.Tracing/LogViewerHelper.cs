namespace SyncPro.Tracing
{
    using System;
    using System.Diagnostics;

    using Microsoft.Win32;

    public static class LogViewerHelper
    {
        public static void LaunchLogViewer(
            string viewerArgs,
            bool closeOnProcessExit)
        {
            using (var softwareKey = Registry.CurrentUser.OpenSubKey("Software"))
            using (RegistryKey jsonLoggerKey = softwareKey.OpenSubKey("JsonLogger"))
            {
                if (jsonLoggerKey == null)
                {
                    throw new Exception("The JsonLogger key was not found under HKEY_CURRENT_USER\\Software");
                }

                object objPath = jsonLoggerKey.GetValue("JsonLogViewerPath");
                if (objPath == null)
                {
                    throw new Exception("The JsonLogViewerPath value was not found or was empty");
                }

                string path = Environment.ExpandEnvironmentVariables(Convert.ToString(objPath));

                string args = viewerArgs;

                if (closeOnProcessExit)
                {
                    args += string.Format(" /closeOnExit {0}", Process.GetCurrentProcess().Id);
                }

                using (Process process = Process.Start(path, args))
                {
                    if (process != null && process.HasExited)
                    {
                        throw new Exception("Failed to start JsonLogViewer");
                    }
                }
            }
        }
    }
}
