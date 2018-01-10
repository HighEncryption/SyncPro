namespace SyncPro.Tracing
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;

    public static class LogViewerHelper
    {
        public static void LaunchLogViewer(
            string viewerArgs,
            bool closeOnProcessExit)
        {
            string[] searchPaths =
            {
                ".",
#if DEBUG
                @"..\..\..\SyncProLogViewer\bin\Debug"
#endif
            };

            string exePath = null;
            string searchBase = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            foreach (string searchPath in searchPaths)
            {
                string path = Path.Combine(searchBase, searchPath, "SyncProLogViewer.exe");
                if (File.Exists(path))
                {
                    exePath = path;
                    break;
                }
            }

            if (exePath != null)
            {
                string args = viewerArgs;

                if (closeOnProcessExit)
                {
                    args += string.Format(" /closeOnExit {0}", Process.GetCurrentProcess().Id);
                }

                using (Process process = Process.Start(exePath, args))
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
