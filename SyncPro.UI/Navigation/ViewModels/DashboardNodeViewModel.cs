namespace SyncPro.UI.Navigation.ViewModels
{
    public class DashboardNodeViewModel : NavigationNodeViewModel
    {
        public DashboardNodeViewModel(NavigationNodeViewModel parent, DashboardViewModel dashboard) 
            : base(parent, dashboard)
        {
            this.Name = "Dashboard";
            this.IconImageSource = "/SyncPro.UI;component/Resources/Graphics/report_open_16.png";
        }
    }
}