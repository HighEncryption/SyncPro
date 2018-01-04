namespace SyncPro.UI.Navigation.ViewModels
{
    using SyncPro.UI.Navigation.MenuCommands;

    public class DashboardNodeViewModel : NavigationNodeViewModel
    {
        public DashboardNodeViewModel(NavigationNodeViewModel parent, DashboardViewModel dashboard) 
            : base(parent, dashboard)
        {
            this.Name = "Dashboard";
            this.IconImageSource = "/SyncPro.UI;component/Resources/Graphics/report_open_16.png";

            this.MenuCommands.Add(new NewRelationshipMenuCommand());
        }
    }
}