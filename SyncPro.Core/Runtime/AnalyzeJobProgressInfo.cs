namespace SyncPro.Runtime
{
    public class AnalyzeJobProgressInfo
    {
        /// <summary>
        /// Described the current activity being performed. This string will be shown as a status when the progress
        /// is indeterminate.
        /// </summary>
        public string Activity { get; }

        /// <summary>
        /// Indicates the value of progress from 0.00 to 1.00. A value of null indicates that progress is indeterminate.
        /// </summary>
        public double? ProgressValue { get; }

        /// <summary>
        /// The total number of files to be synced.
        /// </summary>
        public int FilesTotal { get; }

        /// <summary>
        /// The total number of bytes to be synced.
        /// </summary>
        public long BytesTotal { get; }

        public EntryUpdateInfo UpdateInfo { get; }

        public int SourceAdapterId { get; }

        public AnalyzeJobProgressInfo(
            string activity,
            double? progressValue,
            int adapterId)
        {
            this.Activity = activity;
            this.ProgressValue = progressValue;
            this.SourceAdapterId = adapterId;
        }

        public AnalyzeJobProgressInfo(
            EntryUpdateInfo updateInfo, 
            int sourceAdapterId, 
            string activity,
            int filesTotal, 
            long bytesTotal,
            double? progressValue = null)
        {
            this.Activity = activity;
            this.ProgressValue = progressValue;
            this.SourceAdapterId = sourceAdapterId;
            this.FilesTotal = filesTotal;
            this.BytesTotal = bytesTotal;

            this.UpdateInfo = updateInfo;
        }
    }
}