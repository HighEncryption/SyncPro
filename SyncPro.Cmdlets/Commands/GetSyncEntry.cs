namespace SyncPro.Cmdlets.Commands
{
    using System;
    using System.Data.Entity;
    using System.IO;
    using System.Linq;
    using System.Management.Automation;

    using SyncPro.Data;
    using SyncPro.Runtime;

    [Cmdlet(VerbsCommon.Get, "SyncProEntry")]
    public class GetSyncEntry : PSCmdlet
    {
        [Parameter]
        [Alias("Rid")]
        public Guid RelationshipId { get; set; }

        [Parameter]
        public SwitchParameter Offline { get; set; }

        [Parameter]
        public string AdapterEntryId { get; set; }
        
        [Parameter]
        public string Name { get; set; }

        protected override void ProcessRecord()
        {
            if (this.Offline.ToBool())
            {
                this.ProcessInternalOffline();
            }
            else
            {
                this.ProcessInternalOnline();
            }

        }

        private void ProcessInternalOnline()
        {
            SyncRelationship relationship;

            if (!Global.IsInitialized)
            {
                throw new NotConnectedException();
            }

            if (this.RelationshipId != Guid.Empty)
            {
                relationship = Global.SyncRelationships.FirstOrDefault(
                    r => r.Configuration.RelationshipId == this.RelationshipId);

                if (relationship == null)
                {
                    throw new RelationshipNotFoundException(
                        "The relationship with ID " + this.RelationshipId + " was not found");
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

            using (var db = relationship.GetDatabase())
            {
                foreach (SyncEntry syncEntry in db.Entries.Include(e => e.AdapterEntries))
                {
                    if (!string.IsNullOrWhiteSpace(this.Name) && syncEntry.Name != this.Name)
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(this.AdapterEntryId))
                    {
                        if (syncEntry.AdapterEntries == null ||
                            syncEntry.AdapterEntries.All(e => e.AdapterEntryId != this.AdapterEntryId))
                        {
                            continue;
                        }
                    }

                    this.WriteObject(syncEntry);
                }
            }
        }

        private void ProcessInternalOffline()
        {
            string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appDataRoot = Path.Combine(localAppDataPath, "SyncPro");

            DirectoryInfo appDataRootDir = new DirectoryInfo(appDataRoot);
            SyncRelationship relationship = null;

            foreach (DirectoryInfo relationshipDir in appDataRootDir.GetDirectories())
            {
                Guid guid;
                if (Guid.TryParse(relationshipDir.Name, out guid) && guid == this.RelationshipId)
                {
                    relationship = SyncRelationship.Load(guid);
                    break;
                }
            }

            if (relationship == null)
            {
                throw new RelationshipNotFoundException(
                    "The relationship with ID " + this.RelationshipId + " was not found");
            }

            using (var db = relationship.GetDatabase())
            {
                foreach (SyncEntry syncEntry in db.Entries)
                {
                    this.WriteObject(syncEntry);
                }
            }
        }
    }


    [Cmdlet(VerbsData.Initialize, "SyncProRelationship")]
    public class InitializeSyncProRelationship : PSCmdlet
    {
        [Parameter]
        [Alias("Rid")]
        public Guid RelationshipId { get; set; }



        protected override void ProcessRecord()
        {
        }
    }
}
