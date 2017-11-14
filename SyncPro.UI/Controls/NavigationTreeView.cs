namespace SyncPro.UI.Controls
{
    using System.Windows;
    using System.Windows.Controls;

    public class NavigationTreeView : TreeView
    {
        protected override DependencyObject GetContainerForItemOverride()
        {
            return new NavigationTreeViewItem();
        }

        protected override bool IsItemItsOwnContainerOverride(object item)
        {
            return item is NavigationTreeViewItem;
        }
    }
}