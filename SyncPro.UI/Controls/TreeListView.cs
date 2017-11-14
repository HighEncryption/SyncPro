namespace SyncPro.UI.Controls
{
    using System.Windows;
    using System.Windows.Controls;

    public class TreeListView : TreeView
    {
        protected override DependencyObject GetContainerForItemOverride()
        {
            return new TreeListViewItem();
        }

        protected override bool IsItemItsOwnContainerOverride(object item)
        {
            return item is TreeListViewItem;
        }

        private GridViewColumnCollection columns;

        public GridViewColumnCollection Columns => 
            this.columns ?? (this.columns = new GridViewColumnCollection());
    }
}