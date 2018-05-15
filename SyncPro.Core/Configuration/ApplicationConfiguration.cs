namespace SyncPro.Configuration
{
    using System;
    using System.Security;

    using Newtonsoft.Json;

    using SyncPro.Utility;

    public enum MailService
    {
        Unknown,
        SmtpClient
    }

    public class ApplicationConfiguration
    {
        public const string DefaultFileName = "applicationConfiguration.json";

        public EmailReportConfiguration EmailReporting { get; set; }
    }

    public class EmailReportConfiguration
    {
        public MailService MailService { get; set; }

        public SmtpConfiguration SmtpConfig { get; set; }

        public DateTime ContinuousSyncSendTime { get; set; }

        public string FromAddress { get; set; }

        public string ToAddresses { get; set; }
    }

    public class SmtpConfiguration
    {
        public string Host { get; set; }

        public int Port { get; set; }

        public bool EnableSsl { get; set; }

        public string Username { get; set; }

        [JsonConverter(typeof(SecureStringToProtectedDataConverter))]
        public SecureString Password { get; set; }
    }

}
