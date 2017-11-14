namespace SyncPro.UI.Navigation.ViewModels
{
    using SyncPro.UI.Navigation.MenuCommands;
    using SyncPro.UI.ViewModels;

    public class SearchResultsNodeViewModel : NavigationNodeViewModel
    {
        public SearchResultsNodeViewModel(NavigationNodeViewModel parent, SyncRelationshipViewModel relationship) 
            : base(parent, relationship)
        {
            this.Name = "Search Results";
            this.IconImageSource = "/SyncPro.UI;component/Resources/Graphics/search_16.png";

            this.MenuCommands.Add(new ClosePanelMenuCommand(relationship, this));
        }
    }
}