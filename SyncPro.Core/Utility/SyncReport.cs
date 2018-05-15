namespace SyncPro.Utility
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Net.Mail;
    using System.Text;
    using System.Threading.Tasks;

    using SyncPro.Adapters;
    using SyncPro.Runtime;
    using SyncPro.Tracing;

    public class SyncReport
    {
        private string messageSubject;
        private string messageBody;

        private SyncReport()
        {
        }

        public static SyncReport Create(SyncJob syncRun)
        {
            SyncReport report = new SyncReport
            {
                messageSubject = string.Format(
                    "SyncPro [{0}] sync job for '{1}'", 
                    syncRun.JobResult,
                    syncRun.Relationship.Name)
            };

            int newFileCount = 0;
            int updatedFileCount = 0;
            int movedFileCount = 0;
            int renamedFileCount = 0;
            int deletedFileCount = 0;
            foreach (EntryUpdateInfo updateInfo in syncRun.AnalyzeResult.AdapterResults.SelectMany(r => r.Value.EntryResults))
            {
                if (updateInfo.HasSyncEntryFlag(SyncEntryChangedFlags.IsNew))
                {
                    newFileCount++;
                }
                else if (updateInfo.HasSyncEntryFlag(SyncEntryChangedFlags.IsUpdated))
                {
                    updatedFileCount++;
                }
                else if (updateInfo.HasSyncEntryFlag(SyncEntryChangedFlags.Moved))
                {
                    movedFileCount++;
                }
                else if (updateInfo.HasSyncEntryFlag(SyncEntryChangedFlags.Renamed))
                {
                    renamedFileCount++;
                }
                else if (updateInfo.HasSyncEntryFlag(SyncEntryChangedFlags.Deleted))
                {
                    deletedFileCount++;
                }
            }

            StringBuilder sb = new StringBuilder();

            sb.AppendLine("Relationship: {0}", syncRun.Relationship.Name);
            sb.AppendLine();
            sb.AppendLine("Start time: {0:F}", syncRun.StartTime);
            sb.AppendLine("End time: {0:F}", syncRun.EndTime);
            if (syncRun.EndTime.HasValue)
            {
                TimeSpan elapsed = syncRun.EndTime.Value - syncRun.StartTime;
                sb.AppendLine("Elapsed time: {0}", GetTimeSpanFormat(elapsed));
            }

            sb.AppendLine("Result: {0}", syncRun.JobResult);
            sb.AppendLine();
            sb.AppendLine("Files Processed: {0}", syncRun.FilesTotal);
            sb.AppendLine("Bytes Processed: {0}", syncRun.BytesTotal);
            sb.AppendLine();
            sb.AppendLine("Analysis Results:");
            sb.AppendLine("New items: {0}", newFileCount);
            sb.AppendLine("Updated items: {0}", updatedFileCount);
            sb.AppendLine("Moved items: {0}", movedFileCount);
            sb.AppendLine("Renamed items: {0}", renamedFileCount);
            sb.AppendLine("Deleted items: {0}", deletedFileCount);

            report.messageBody = sb.ToString();

            return report;
        }

        private static string GetTimeSpanFormat(TimeSpan ts)
        {
            StringBuilder sb = new StringBuilder();
            if (ts.Days > 0)
            {
                sb.AppendFormat("{0} days,", ts.Days);
            }

            if (ts.Hours > 0 || ts.Days > 0)
            {
                sb.AppendFormat("{0} hours,", ts.Hours);
            }

            if (ts.Minutes > 0 || ts.Hours > 0 || ts.Days > 0)
            {
                sb.AppendFormat("{0} minutes,", ts.Minutes);
            }

            sb.AppendFormat("{0} seconds", ts.Seconds);

            return sb.ToString();
        }

        public async Task SendAsync()
        {
            using (SmtpClient client = new SmtpClient())
            {
                client.Port = Global.AppConfig.EmailReporting.SmtpConfig.Port;
                client.Host = Global.AppConfig.EmailReporting.SmtpConfig.Host;
                client.EnableSsl = Global.AppConfig.EmailReporting.SmtpConfig.EnableSsl;
                client.DeliveryMethod = SmtpDeliveryMethod.Network;
                client.Credentials = new NetworkCredential()
                {
                    SecurePassword = Global.AppConfig.EmailReporting.SmtpConfig.Password,
                    UserName = Global.AppConfig.EmailReporting.SmtpConfig.Username
                };

                MailMessage message = new MailMessage(
                    Global.AppConfig.EmailReporting.FromAddress,
                    Global.AppConfig.EmailReporting.ToAddresses,
                    this.messageSubject,
                    this.messageBody)
                {
                    BodyEncoding = Encoding.UTF8
                };

                await client.SendMailAsync(message);
            }
        }
    }
}
