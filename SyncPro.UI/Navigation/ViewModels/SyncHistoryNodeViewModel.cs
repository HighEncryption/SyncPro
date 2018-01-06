namespace SyncPro.UI.Navigation.ViewModels
{
    using System.Collections.Specialized;
    using System.Linq;

    using SyncPro.UI.ViewModels;

    public class SyncHistoryNodeViewModel : NavigationNodeViewModel
    {
        private readonly SyncRelationshipViewModel relationship;

        public SyncHistoryNodeViewModel(NavigationNodeViewModel parent, SyncRelationshipViewModel relationship) 
            : base(parent, relationship)
        {
            this.relationship = relationship;
            this.Name = "Synchronization History";
            this.IconImageSource = "/SyncPro.UI;component/Resources/Graphics/history_16.png";

            foreach (SyncJobViewModel syncJobViewModel in relationship.SyncJobHistory)
            {
                this.AddSyncJobHistory(syncJobViewModel);
            }

            relationship.SyncJobHistory.CollectionChanged += (sender, args) =>
            {
                if (args.Action == NotifyCollectionChangedAction.Add)
                {
                    foreach (SyncJobViewModel syncJobViewModel in args.NewItems.OfType<SyncJobViewModel>())
                    {
                        App.DispatcherInvoke(() =>
                        {
                            this.AddSyncJobHistory(syncJobViewModel);
                        });
                    }
                }
            };
        }

        private void AddSyncJobHistory(SyncJobViewModel syncJobViewModel)
        {
            SyncJobPanelViewModel syncJobPanel = new SyncJobPanelViewModel(this.relationship)
            {
                SyncJobViewModel = syncJobViewModel
            };

            this.Children.Add(new SyncJobNodeViewModel(this, syncJobPanel));
            this.RaisePropertyChanged(nameof(this.Children));
        }
    }
}
