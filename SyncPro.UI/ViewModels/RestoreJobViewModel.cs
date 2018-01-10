namespace SyncPro.UI.ViewModels
{
    using System;
    using System.Diagnostics;

    using SyncPro.Runtime;
    using SyncPro.UI.Converters;

    public class RestoreJobViewModel : JobViewModel
    {
        public RestoreJob RestoreJob => (RestoreJob)this.Job;

        #region ViewModel properties

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool showDiscreteProgress;

        public bool ShowDiscreteProgress
        {
            get { return this.showDiscreteProgress; }
            set { this.SetProperty(ref this.showDiscreteProgress, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private TimeSpan timeElapsed;

        public TimeSpan TimeElapsed
        {
            get { return this.timeElapsed; }
            set { this.SetProperty(ref this.timeElapsed, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private TimeSpan timeRemaining;

        public TimeSpan TimeRemaining
        {
            get { return this.timeRemaining; }
            set { this.SetProperty(ref this.timeRemaining, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private double progressValue;

        public double ProgressValue
        {
            get { return this.progressValue; }
            set { this.SetProperty(ref this.progressValue, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int filesCompleted;

        public int FilesCompleted
        {
            get { return this.filesCompleted; }
            set { this.SetProperty(ref this.filesCompleted, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private long bytesCompleted;

        public long BytesCompleted
        {
            get { return this.bytesCompleted; }
            set { this.SetProperty(ref this.bytesCompleted, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int filesRemaining;

        public int FilesRemaining
        {
            get { return this.filesRemaining; }
            set { this.SetProperty(ref this.filesRemaining, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private long bytesRemaining;

        public long BytesRemaining
        {
            get { return this.bytesRemaining; }
            set { this.SetProperty(ref this.bytesRemaining, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string throughput;

        public string Throughput
        {
            get { return this.throughput; }
            set { this.SetProperty(ref this.throughput, value); }
        }

        #endregion

        public RestoreJobViewModel(RestoreJob job, SyncRelationshipViewModel relationshipViewModel, bool loadFromHistory)
            : base(job, relationshipViewModel)
        {
            this.RestoreJob.ProgressChanged += this.RestoreJobOnProgressChanged;
        }

        /// <summary>
        /// During an active sync job, this methods is invoked on progress changed (aka when a change is detected).
        /// </summary>
        /// <param name="sender">The sender</param>
        /// <param name="syncJobProgressInfo">The progressInfo object</param>
        private void RestoreJobOnProgressChanged(object sender, RestoreJobProgressInfo syncJobProgressInfo)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                if (!double.IsInfinity(syncJobProgressInfo.ProgressValue))
                {
                    this.ShowDiscreteProgress = true;

                    this.ProgressValue = syncJobProgressInfo.ProgressValue;

                    this.BytesCompleted = syncJobProgressInfo.BytesCompleted;
                    this.BytesRemaining = syncJobProgressInfo.BytesTotal - syncJobProgressInfo.BytesCompleted;

                    this.FilesCompleted = syncJobProgressInfo.FilesCompleted;
                    this.FilesRemaining = syncJobProgressInfo.FilesTotal - syncJobProgressInfo.FilesCompleted;

                    this.TimeElapsed = DateTime.Now.Subtract(this.StartTime);

                    if (syncJobProgressInfo.BytesPerSecond > 0)
                    {
                        this.TimeRemaining = TimeSpan.FromSeconds(
                            this.BytesRemaining / syncJobProgressInfo.BytesPerSecond);
                    }
                    else
                    {
                        this.TimeRemaining = TimeSpan.Zero;
                    }

                    this.Throughput = FileSizeConverter.Convert(syncJobProgressInfo.BytesPerSecond, 2) + " per second";
                }
            });
        }
    }
}