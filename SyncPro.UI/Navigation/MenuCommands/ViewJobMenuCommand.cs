namespace SyncPro.UI.Navigation.MenuCommands
{
    using SyncPro.UI.ViewModels;

    public class ViewJobMenuCommand : NavigationItemMenuCommand
    {
        private readonly JobViewModel jobViewModel;

        public ViewJobMenuCommand(JobViewModel jobViewModel)
            : base("VIEW JOB", "/SyncPro.UI;component/Resources/Graphics/list_16.png")
        {
            this.jobViewModel = jobViewModel;
        }

        protected override bool CanInvokeCommand(object obj)
        {
            return this.jobViewModel.ShowJobCommand.CanExecute(obj);
        }

        protected override void InvokeCommand(object obj)
        {
            this.jobViewModel.ShowJobCommand.Execute(obj);
        }
    }
}