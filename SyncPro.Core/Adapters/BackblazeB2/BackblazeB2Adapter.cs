namespace SyncPro.Adapters.BackblazeB2
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    using SyncPro.Data;
    using SyncPro.Runtime;

    public class BackblazeB2Adapter : AdapterBase
    {
        public static readonly Guid TargetTypeId = Guid.Parse("771fbdb2-cbb8-457e-a552-ca1f02c9d707");

        private BackblazeB2Client backblazeClient;

        public BackblazeB2Adapter(SyncRelationship relationship, BackblazeB2AdapterConfiguration configuration) 
            : base(relationship, configuration)
        {
        }

        public BackblazeB2Adapter(SyncRelationship relationship)
            : base(relationship, new BackblazeB2AdapterConfiguration())
        {
        }

        public string AccountId { get; set; }

        public BackblazeB2AdapterConfiguration TypedConfiguration
            => (BackblazeB2AdapterConfiguration) this.Configuration;

        public event EventHandler InitializationComplete;

        public bool IsInitialized { get; private set; }

        public override Guid GetTargetTypeId()
        {
            throw new NotImplementedException();
        }

        public override Task<SyncEntry> CreateRootEntry()
        {
            throw new NotImplementedException();
        }

        public override Task<IAdapterItem> GetRootFolder()
        {
            throw new NotImplementedException();
        }

        public override Task CreateItemAsync(SyncEntry entry)
        {
            throw new NotImplementedException();
        }

        public override Stream GetReadStreamForEntry(SyncEntry entry)
        {
            throw new NotImplementedException();
        }

        public override Stream GetWriteStreamForEntry(SyncEntry entry, long length)
        {
            throw new NotImplementedException();
        }

        public override void UpdateItem(EntryUpdateInfo updateInfo, SyncEntryChangedFlags changeFlags)
        {
            throw new NotImplementedException();
        }

        public override void DeleteItem(SyncEntry entry)
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<IAdapterItem> GetAdapterItems(IAdapterItem folder)
        {
            throw new NotImplementedException();
        }

        public override bool IsEntryUpdated(SyncEntry childEntry, IAdapterItem adapterItem, out EntryUpdateResult result)
        {
            throw new NotImplementedException();
        }

        public override SyncEntry CreateSyncEntryForAdapterItem(IAdapterItem item, SyncEntry parentEntry)
        {
            throw new NotImplementedException();
        }
        public async Task InitializeClient()
        {
            this.backblazeClient = new BackblazeB2Client(
                this.TypedConfiguration.AccountId,
                this.TypedConfiguration.ApplicationKey,
                this.TypedConfiguration.ConnectionInfo);

            this.backblazeClient.ConnectionInfoChanged += (s, e) =>
            {
                this.TypedConfiguration.ConnectionInfo = e.ConnectionInfo;
                this.AccountId = e.AccountId;
                this.SaveConfiguration();
            };

            // Call the initization method to build the connection info if needed.
            await this.backblazeClient.InitializeAsync().ConfigureAwait(false);

            // List the buckets in the account. This will have side effect fo refreshing the auth token 
            // and throw an exception if the refresh token fails.
            await this.backblazeClient.ListBucketsAsync().ConfigureAwait(false);

            this.IsInitialized = true;
            this.InitializationComplete?.Invoke(this, new EventArgs());
        }

        public override async Task InitializeAsync()
        {
            try
            {
                await this.InitializeClient().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                Exception selectedException = exception;
                AggregateException aggEx = exception as AggregateException;
                if (aggEx?.InnerExceptions.Count == 1 && aggEx.InnerException != null)
                {
                    selectedException = aggEx.InnerException;
                }

                this.FaultInformation = new BackblazeB2InitializationFault(this, selectedException);
            }
        }

        public override void SaveConfiguration()
        {
            base.SaveConfiguration();
        }
    }
}
