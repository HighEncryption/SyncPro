namespace SyncPro.UI.Navigation.ViewModels
{
    public class LazyLoadPlaceholderNodeViewModel : NavigationNodeViewModel
    {
        public static LazyLoadPlaceholderNodeViewModel Instance
            = new LazyLoadPlaceholderNodeViewModel();

        public LazyLoadPlaceholderNodeViewModel() 
            : base(null, null)
        {
            this.Name = "Loading...";
            this.IconImageSource = "/SyncPro.UI;component/Resources/Graphics/folder_open_16.png";
        }
    }
}