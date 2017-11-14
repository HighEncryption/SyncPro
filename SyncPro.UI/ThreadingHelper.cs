namespace SyncPro.UI
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public static class ThreadingHelper
    {
        public static void StartBackgroundTask(Action<TaskScheduler, CancellationToken> backgroundAction)
        {
            StartBackgroundTask(backgroundAction, null);
        }

        public static void StartBackgroundTask(Action<TaskScheduler, CancellationToken> backgroundAction, Action<bool> completeAction)
        {
            var tokenSource = new CancellationTokenSource();
            CancellationToken ct = tokenSource.Token;

            TaskScheduler uiSyncContext = TaskScheduler.FromCurrentSynchronizationContext();
            Task backgroundTask = Task.Factory.StartNew(() => backgroundAction(uiSyncContext, ct), ct);

            if (completeAction != null)
            {
                backgroundTask.ContinueWith(delegate { completeAction(ct.IsCancellationRequested); }, uiSyncContext);
            }
            else
            {
                backgroundTask.ContinueWith(
                    task =>
                    {
                        if (task.Exception != null)
                        {
                            throw new Exception("Unhandled exception!", task.Exception);
                        }
                    },
                    ct);
            }
        }
    }
}