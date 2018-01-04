namespace SyncPro.Runtime
{
    public class SyncJobProgressInfo
    {
        /// <summary>
        /// Indicates the value of progress from 0.00 to 1.00. A value of  -1.0 indicates that progress is indeterminate.
        /// </summary>
        public double ProgressValue { get; }

        /// <summary>
        /// The total number of files to be synced.
        /// </summary>
        public int FilesTotal { get; }

        /// <summary>
        /// The completed number of files to be synced.
        /// </summary>
        public int FilesCompleted { get; }

        /// <summary>
        /// The total number of bytes to be synced.
        /// </summary>
        public long BytesTotal { get; }

        /// <summary>
        /// The completed number of bytes to be synced.
        /// </summary>
        public long BytesCompleted { get; }

        public int BytesPerSecond { get; }

        public string Message { get; }

        public SyncJobStage Stage { get; }

        public EntryUpdateInfo UpdateInfo { get; }

        public SyncJobProgressInfo(SyncJobStage stage, string message)
        {
            this.Stage = stage;
            this.Message = message;
        }

        public SyncJobProgressInfo(EntryUpdateInfo updateInfo, int filesTotal, long bytesTotal)
        {
            this.Stage = SyncJobStage.Analyze;
            this.ProgressValue = double.PositiveInfinity;
            this.FilesTotal = filesTotal;
            this.BytesTotal = bytesTotal;

            this.UpdateInfo = updateInfo;
        }

        public SyncJobProgressInfo(
            EntryUpdateInfo updateInfo,
            int filesTotal,
            int filesCompleted,
            long bytesTotal,
            long bytesCompleted,
            int bytesPerSecond)
        {
            this.Stage = SyncJobStage.Sync;
            this.UpdateInfo = updateInfo;

            this.ProgressValue = (double)bytesCompleted / (double)bytesTotal;

            this.FilesTotal = filesTotal;
            this.FilesCompleted = filesCompleted;
            this.BytesTotal = bytesTotal;
            this.BytesCompleted = bytesCompleted;
            this.BytesPerSecond = bytesPerSecond;
        }
    }
}