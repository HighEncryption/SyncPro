namespace SyncPro.UI.Navigation.ViewModels
{
    using SyncPro.Runtime;

    public class SyncRunNodeViewModel : NavigationNodeViewModel
    {
        private readonly SyncRunPanelViewModel syncRunPanel;

        public SyncRunNodeViewModel(NavigationNodeViewModel parent, SyncRunPanelViewModel syncRunPanel) 
            : base(parent, syncRunPanel)
        {
            this.syncRunPanel = syncRunPanel;

            if (syncRunPanel.SyncRun != null)
            {
                this.Name = syncRunPanel.SyncRun.StartTime.ToString("g");
            }

            this.SetIconImage();
        }

        private void SetIconImage()
        {
            if (this.syncRunPanel.SyncRun == null)
            {
                this.IconImageSource = "/SyncPro.UI;component/Resources/Graphics/list_16.png";
                return;
            }

            switch (this.syncRunPanel.SyncRun.SyncRunResult)
            {
                case SyncRunResult.Success:
                    this.IconImageSource = "/SyncPro.UI;component/Resources/Graphics/list_ok_16.png";
                    break;
                case SyncRunResult.Warning:
                    this.IconImageSource = "/SyncPro.UI;component/Resources/Graphics/list_warn_16.png";
                    break;
                case SyncRunResult.Error:
                    this.IconImageSource = "/SyncPro.UI;component/Resources/Graphics/list_error_16.png";
                    break;
                case SyncRunResult.NotRun:
                    this.IconImageSource = "/SyncPro.UI;component/Resources/Graphics/list_16.png";
                    break;
                default:
                    this.IconImageSource = "/SyncPro.UI;component/Resources/Graphics/list_help_16.png";
                    break;
            }
        }

        protected override void OnIsSelected()
        {
            this.syncRunPanel.SyncRun?.BeginLoad();
        }
    }
}