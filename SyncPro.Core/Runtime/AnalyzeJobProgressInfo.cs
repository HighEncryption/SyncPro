namespace SyncPro.Runtime
{
    public class AnalyzeJobProgressInfo
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
        /// The total number of bytes to be synced.
        /// </summary>
        public long BytesTotal { get; }

        public EntryUpdateInfo UpdateInfo { get; }

        public AnalyzeJobProgressInfo(EntryUpdateInfo updateInfo, int filesTotal, long bytesTotal)
        {
            this.ProgressValue = double.PositiveInfinity;
            this.FilesTotal = filesTotal;
            this.BytesTotal = bytesTotal;

            this.UpdateInfo = updateInfo;
        }
    }
}