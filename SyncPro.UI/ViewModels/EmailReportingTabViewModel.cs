namespace SyncPro.UI.ViewModels
{
    using System.Diagnostics;
    using System.Security;

    using SyncPro.Configuration;

    public class EmailReportingTabViewModel : TabPageViewModelBase
    {
        public EmailReportingTabViewModel(ITabControlHostViewModel tabControlHost)
        : base(tabControlHost)
        {
        }

        public override string NavTitle => "Email";

        public override string PageTitle => "Email Reporting Settings";

        public override string TabItemImageSource => "/SyncPro.UI;component/Resources/Graphics/mail_20.png";

        public override string PageSubText => "This is where the text would go that talks about what goes on this page.";


        public override void LoadContext()
        {
        }

        public override void SaveContext()
        {
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool enableEmail;

        public bool EnableEmail
        {
            get => this.enableEmail;
            set => this.SetProperty(ref this.enableEmail, value);
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string smtpHost;

        public string SmtpHost
        {
            get => this.smtpHost;
            set => this.SetProperty(ref this.smtpHost, value);
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int smtpPort;

        public int SmtpPort
        {
            get => this.smtpPort;
            set => this.SetProperty(ref this.smtpPort, value);
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool smtpEnableSsl;

        public bool SmtpEnableSsl
        {
            get => this.smtpEnableSsl;
            set => this.SetProperty(ref this.smtpEnableSsl, value);
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string smtpUsername;

        public string SmtpUsername
        {
            get => this.smtpUsername;
            set => this.SetProperty(ref this.smtpUsername, value);
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private SecureString smtpPassword;

        public SecureString SmtpPassword
        {
            get => this.smtpPassword;
            set => this.SetProperty(ref this.smtpPassword, value);
        }

        public string FromAddress { get; set; }

        public string ToAddresses { get; set; }

        public void Load()
        {
            if (Global.AppConfig.EmailReporting == null || 
                Global.AppConfig.EmailReporting.SmtpConfig == null)
            {
                this.EnableEmail = false;

                // Nothing to load. Should we set fields to defaults?
                return;
            }

            this.EnableEmail = true;

            this.FromAddress = Global.AppConfig.EmailReporting.FromAddress;
            this.ToAddresses = Global.AppConfig.EmailReporting.ToAddresses;

            this.SmtpHost = Global.AppConfig.EmailReporting.SmtpConfig.Host;
            this.SmtpPort = Global.AppConfig.EmailReporting.SmtpConfig.Port;
            this.SmtpEnableSsl = Global.AppConfig.EmailReporting.SmtpConfig.EnableSsl;
            this.SmtpUsername = Global.AppConfig.EmailReporting.SmtpConfig.Username;
            this.SmtpPassword = Global.AppConfig.EmailReporting.SmtpConfig.Password;
        }

        public void Save()
        {
            if (this.EnableEmail == false)
            {
                Global.AppConfig.EmailReporting = null;
                return;
            }

            if (Global.AppConfig.EmailReporting == null)
            {
                Global.AppConfig.EmailReporting = new EmailReportConfiguration();
            }

            Global.AppConfig.EmailReporting.FromAddress = this.FromAddress;
            Global.AppConfig.EmailReporting.ToAddresses = this.ToAddresses;

            Global.AppConfig.EmailReporting.SmtpConfig = new SmtpConfiguration
            {
                EnableSsl = this.SmtpEnableSsl,
                Host = this.SmtpHost,
                Port = this.SmtpPort,
                Username = this.SmtpUsername,
                Password = this.SmtpPassword
            };
        }
    }
}
