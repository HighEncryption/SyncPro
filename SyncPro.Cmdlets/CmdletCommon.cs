namespace SyncPro.Cmdlets
{
    using System;
    using System.Linq;

    using SyncPro.Runtime;

    internal static class CmdletCommon
    {
        public static SyncRelationship GetSyncRelationship(Guid relationshipId)
        {
            SyncRelationship relationship;

            if (!Global.IsInitialized)
            {
                throw new NotConnectedException();
            }

            if (relationshipId != Guid.Empty)
            {
                relationship = Global.SyncRelationships.FirstOrDefault(
                    r => r.Configuration.RelationshipId == relationshipId);

                if (relationship == null)
                {
                    throw new RelationshipNotFoundException(
                        "The relationship with ID " + relationshipId + " was not found");
                }
            }
            else if (Global.SelectedSyncRelationship != null)
            {
                relationship = Global.SelectedSyncRelationship;
            }
            else
            {
                throw new NoSelectedRelationshipException();
            }

            return relationship;
        }
    }
}
