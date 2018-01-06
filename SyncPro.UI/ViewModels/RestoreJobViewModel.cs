namespace SyncPro.UI.ViewModels
{
    using SyncPro.Runtime;

    public class RestoreJobViewModel : JobViewModel
    {
        public RestoreJobViewModel(RestoreJob job, SyncRelationshipViewModel relationshipViewModel, bool loadFromHistory)
            : base(job, relationshipViewModel)
        {
        }
    }
}