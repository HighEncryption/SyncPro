namespace SyncPro.UI.Controls
{
    using System.Windows;
    using System.Windows.Controls;

    public class TreeListViewItem : TreeViewItem
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

                TreeListViewItem parent = ItemsControlFromItemContainer(this) as TreeListViewItem;
                this.level = parent?.Level + 1 ?? 0;

                return this.level;
            }
        }

        protected override DependencyObject GetContainerForItemOverride()
        {
            return new TreeListViewItem();
        }

        protected override bool IsItemItsOwnContainerOverride(object item)
        {
            return item is TreeListViewItem;
        }
    }
}