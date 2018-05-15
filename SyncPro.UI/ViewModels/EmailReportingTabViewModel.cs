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
        private MailService mailService;

        public MailService MailService
        {
            get => this.mailService;
            set => this.SetProperty(ref this.mailService, value);
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

    }
}
