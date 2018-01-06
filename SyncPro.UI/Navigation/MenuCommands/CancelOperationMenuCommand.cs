namespace SyncPro.UI.Navigation.MenuCommands
{
    public class CancelOperationMenuCommand : NavigationItemMenuCommand
    {
        private readonly ICancelJobPanelViewModel jobPanelViewModel;

        public CancelOperationMenuCommand(ICancelJobPanelViewModel jobPanelViewModel)
            : base("CANCEL", "/SyncPro.UI;component/Resources/Graphics/stop_16.png")
        {
            this.jobPanelViewModel = jobPanelViewModel;
        }

        protected override bool CanInvokeCommand(object obj)
        {
            return this.jobPanelViewModel.CancelJobCommand.CanExecute(null);
        }

        protected override void InvokeCommand(object obj)
        {
            this.jobPanelViewModel.CancelJobCommand.Execute(null);
        }
    }
}