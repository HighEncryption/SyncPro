namespace SyncProLogViewer
{
    using System;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Windows.Controls;
    using System.Windows.Input;

    using SyncProLogViewer.ViewModels;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        public MainWindow()
        {
            this.InitializeComponent();
        }

        private void EntryListView_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListBox listBox = sender as ListBox;
            listBox?.ScrollIntoView(listBox.SelectedItem);
        }

        private void listViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ListViewItem item = sender as ListViewItem;
            LogEntry logEntry = item?.Content as LogEntry;
            var vm = this.DataContext as MainWindowViewModel;
            if (vm == null || logEntry == null || string.IsNullOrWhiteSpace(logEntry.ActivityPath))
            {
                return;
            }

            var elements = logEntry.ActivityPath.Split(new[] {"/"}, StringSplitOptions.RemoveEmptyEntries);
            ObservableCollection<ActivityInfo> list = vm.TopLevelActivities;
            ActivityInfo activityInfo = null;
            for(int i = 0; i < elements.Length; i++)
            {
                int id = int.Parse(elements[i]);
                activityInfo = list.FirstOrDefault(a => a.Id == id);
                if (activityInfo == null)
                {
                    // Not Found?
                    return;
                }

                list = activityInfo.Children;
            }

            vm.SelectedActivityInfo = activityInfo;
        }
    }
}
