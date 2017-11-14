namespace SyncPro.UI.Controls
{
    using System.Windows;
    using System.Windows.Controls;

    public class NavigationTreeViewItem : TreeViewItem
    {
        private int level = -1;

        public int Level
        {
            get
            {
                if (this.level != -1)
                {
                    return this.level;
                }

                NavigationTreeViewItem parent = ItemsControlFromItemContainer(this) as NavigationTreeViewItem;
                this.level = (parent != null) ? parent.Level + 1 : 0;

                return this.level;
            }
        }

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