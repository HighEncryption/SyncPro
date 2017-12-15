namespace SyncPro.PowerShell
{
    using System;
    using System.Diagnostics;
    using System.Management.Automation.Runspaces;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Win32.SafeHandles;

    using SyncPro.Tracing;

    public static class RuntimeHost
    {
        private static CancellationTokenSource cancellationTokenSource;

        public static Task Start()
        {
            cancellationTokenSource = new CancellationTokenSource();

            return Task.Factory.StartNew(
                    StartRuntime,
                    cancellationTokenSource.Token)
                .ContinueWith(RuntimeContinuation);
        }

        private static void RuntimeContinuation(Task task)
        {
            if (task.Exception != null)
            {
                Logger.LogException(task.Exception, "PowerShell runtime exited with exception.");
            }
        }

        public static void Stop()
        {
            cancellationTokenSource?.Cancel();
        }

        private static void StartRuntime()
        {
            // Get the path to the PowerShell executable on the user's machine
            string powerShellPath = Environment.ExpandEnvironmentVariables(
                @"%SystemRoot%\system32\WindowsPowerShell\v1.0\powershell.exe");

            // Get the current process PID for connecting the new window
            var pid = Process.GetCurrentProcess().Id;

            // Build the encoded command to pass to the PowerShell window
            string initialCommand = string.Format("Enter-PSHostProcess -Id {0}", pid);
            string encodedCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(initialCommand));

            Logger.Debug("Starting PowerShell process with path: " + powerShellPath);
            Logger.Debug("Starting PowerShell process with command: " + initialCommand);

            InitialSessionState iss = InitialSessionState.CreateDefault();
            using (Runspace myRunSpace = RunspaceFactory.CreateRunspace(iss))
            {
                // Open the runspace.
                myRunSpace.Open();

                Process powerShellProcess = Process.Start(
                    powerShellPath,
                    string.Format("-NoExit -EncodedCommand \"{0}\"", encodedCommand));

                Console.WriteLine("READY");

                SafeWaitHandle processWaitHandle = new SafeWaitHandle(
                    powerShellProcess.Handle, 
                    false);

                WaitHandle.WaitAny(
                    new WaitHandle[]
                    {
                        new ManualResetEvent(true) { SafeWaitHandle = processWaitHandle },
                        cancellationTokenSource.Token.WaitHandle
                    });

                // Wait for cancellation to close the runtime
                cancellationTokenSource.Token.WaitHandle.WaitOne();

                // close the runspace to free resources.
                myRunSpace.Close();
            }
        }
    }
}
