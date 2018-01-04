using System.ComponentModel;

namespace SyncPro.UI.Navigation.ViewModels
{
    using System.Collections.Generic;

    public interface IFolderNodeViewModel : INotifyPropertyChanged
    {
        IList<SyncEntryViewModel> SelectedChildEntries { get; set; }
    }
}