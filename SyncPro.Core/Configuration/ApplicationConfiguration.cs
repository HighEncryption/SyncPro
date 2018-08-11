namespace SyncPro.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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

        public bool AcceptUsage { get; set; }

        public List<WindowConfiguration> WindowsConfigurations { get; set; }

        public EmailReportConfiguration EmailReporting { get; set; }

        public WindowConfiguration GetOrCreateWindowConfig(
            string id, 
            Func<WindowConfiguration> createWindowConfig)
        {
            var config = this.WindowsConfigurations.FirstOrDefault(w => w.Id == id);
            if (config == null)
            {
                config = createWindowConfig();
                config.Id = id;
                this.WindowsConfigurations.Add(config);
            }

            return config;
        }

        public ApplicationConfiguration()
        {
            this.WindowsConfigurations = new List<WindowConfiguration>();
        }
    }

    public class WindowConfiguration
    {
        public string Id { get; set; }

        public int Height { get; set; }

        public int Width { get; set; }
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
