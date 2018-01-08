namespace SyncPro.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;

    using SyncPro.Adapters;
    using SyncPro.Configuration;
    using SyncPro.Data;
    using SyncPro.Tracing;

    public class RestoreJob : JobBase
    {
        private readonly IList<SyncEntry> syncEntries;
        private readonly string restorePath;
        private X509Certificate2 encryptionCertificate;

        public RestoreJob(SyncRelationship relationship, IList<SyncEntry> syncEntries, string restorePath) 
            : base(relationship)
        {
            Pre.ThrowIfArgumentNull(relationship, nameof(relationship));
            Pre.ThrowIfArgumentNull(syncEntries, nameof(syncEntries));

            Pre.ThrowIfStringNullOrWhiteSpace(restorePath, nameof(restorePath));

            this.syncEntries = syncEntries;
            this.restorePath = restorePath;
        }

        protected override async Task ExecuteTask()
        {
            if (!this.syncEntries.Any())
            {
                return;
            }

            RestoreResult result = new RestoreResult();

            RestoreOnlyWindowsFileSystemAdapterConfiguration adapterConfig = new RestoreOnlyWindowsFileSystemAdapterConfiguration
            {
                RootDirectory = this.restorePath
            };

            RestoreOnlyWindowsFileSystemAdapter destAdapter =
                new RestoreOnlyWindowsFileSystemAdapter(this.Relationship, adapterConfig);

            if (this.Relationship.EncryptionMode != Configuration.EncryptionMode.None)
            {
                X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadOnly);

                var cert = store.Certificates.Find(
                    X509FindType.FindByThumbprint,
                    this.Relationship.EncryptionCertificateThumbprint,
                    false);

                this.encryptionCertificate = cert[0];
            }

            using (var db = this.Relationship.GetDatabase())
            {
                foreach (SyncEntry syncEntry in this.syncEntries)
                {
                    if (this.CancellationToken.IsCancellationRequested)
                    {
                        result.Cancelled = true;
                        return;
                    }

                    RestoreItemResult itemResult = new RestoreItemResult
                    {
                        Entry = syncEntry
                    };

                    string message = string.Empty;

                    try
                    {
                        AdapterBase sourceAdapter = this.Relationship.Adapters.FirstOrDefault(
                            a => a.Configuration.Flags != AdapterFlags.Originator);

                        await this.RestoreFileAsync(
                                syncEntry,
                                sourceAdapter,
                                destAdapter,
                                db)
                            .ConfigureAwait(false);

                        message = "The change was successfully synchronized";
                    }
                    catch (TaskCanceledException)
                    {
                        result.Cancelled = true;
                        Logger.Warning("Processing was cancelled");

                        message = "The change was cancelled during processing";
                        itemResult.ErrorMessage = "Processing was cancelled";
                        itemResult.State = EntryUpdateState.Failed;
                    }
                    catch (Exception exception)
                    {
                        Logger.Warning(
                            "Processing failed with {0}: {1}",
                            exception.GetType().FullName,
                            exception.Message);

                        message = "An error occurred while synchronzing the changed.";
                        itemResult.ErrorMessage = exception.Message;
                        itemResult.State = EntryUpdateState.Failed;
                    }
                    finally
                    {
                        Logger.ChangeSynchronzied(
                            Logger.BuildEventMessageWithProperties(
                                message,
                                new Dictionary<string, object>()));
                    }
                }
            }
        }

        private async Task RestoreFileAsync(
            SyncEntry syncEntry,
            AdapterBase fromAdapter,
            AdapterBase toAdapter,
            SyncDatabase db)
        {
            string newPath = System.IO.Path.Combine(this.restorePath, syncEntry.GetRelativePath(db, "/"));

            EntryUpdateInfo updateInfo = new EntryUpdateInfo(
                syncEntry,
                fromAdapter, 
                SyncEntryChangedFlags.Restored,
                syncEntry.GetRelativePath(db, "/"));

            FileCopyHelper fileCopyHelper = new FileCopyHelper(
                this.Relationship,
                fromAdapter,
                toAdapter,
                updateInfo,
                null,
                this.encryptionCertificate,
                this.CancellationToken,
                this.CopyProgressChanged);

            Logger.Debug("Creating item with content");
            await fileCopyHelper.CopyFileAsync().ConfigureAwait(false);

        }

        private void CopyProgressChanged(CopyProgressInfo obj)
        {
            throw new NotImplementedException();
        }
    }

    public class RestoreResult
    {
        public List<RestoreItemResult> ItemResults { get; }

        public bool Cancelled { get; set; }

        public RestoreResult()
        {
            this.ItemResults = new List<RestoreItemResult>();
        }
    }

    public class RestoreItemResult
    {
        public SyncEntry Entry { get; set; }

        public EntryUpdateState State { get; set; }

        public string ErrorMessage { get; set; }
    }

    internal class CopyProgressInfo
    {
        public CopyProgressInfo(long bytesCopied, EntryUpdateInfo entryUpdateInfo)
        {
            this.BytesCopied = bytesCopied;
            this.EntryUpdateInfo = entryUpdateInfo;
        }

        public long BytesCopied { get; set; }

        public EntryUpdateInfo EntryUpdateInfo { get; set; }
    }

    internal class FileCopyHelper
    {
        private readonly SyncRelationship relationship;
        private readonly AdapterBase fromAdapter;
        private readonly AdapterBase toAdapter;
        private readonly EntryUpdateInfo updateInfo;
        private readonly ThrottlingManager throttlingManager;
        private readonly X509Certificate2 encryptionCertificate;
        private readonly CancellationToken cancellationToken;
        private readonly Action<CopyProgressInfo> progressChanged;
        private readonly Stopwatch syncProgressUpdateStopwatch;

        private EncryptionManager encryptionManager;
        private int bytesCompleted;

        /// <summary>
        /// The buffer size used for copying data between adapters (currently 64k).
        /// </summary>
        private const int transferBufferSize = 0x10000;

        public FileCopyHelper(
            SyncRelationship relationship,
            AdapterBase fromAdapter,
            AdapterBase toAdapter,
            EntryUpdateInfo updateInfo,
            ThrottlingManager throttlingManager,
            X509Certificate2 encryptionCertificate,
            CancellationToken cancellationToken,
            Action<CopyProgressInfo> progressChanged)
        {
            this.relationship = relationship;
            this.fromAdapter = fromAdapter;
            this.toAdapter = toAdapter;
            this.updateInfo = updateInfo;
            this.throttlingManager = throttlingManager;
            this.encryptionCertificate = encryptionCertificate;
            this.cancellationToken = cancellationToken;
            this.progressChanged = progressChanged;

            this.syncProgressUpdateStopwatch = new Stopwatch();
        }

        /// <summary>
        /// Copy a file from the source adapter to the destination adapter
        /// </summary>
        /// <returns>The async task</returns>
        public async Task CopyFileAsync()
        {
            Stream fromStream = null;
            Stream toStream = null;

            this.syncProgressUpdateStopwatch.Start();

            try
            {
                // Assume the original size of the file will be the write size. If encryption is enabled, the value
                // will be updated below.
                long writeStreamLength = this.updateInfo.Entry.OriginalSize;

                if (this.relationship.EncryptionMode == EncryptionMode.Encrypt)
                {
                    short padding;
                    writeStreamLength = EncryptionManager.CalculateEncryptedFileSize(
                        this.updateInfo.Entry.OriginalSize,
                        out padding);
                }
                else if (this.relationship.EncryptionMode == EncryptionMode.Decrypt)
                {
                    short padding;
                    writeStreamLength = EncryptionManager.CalculateDecryptedFileSize(
                        this.updateInfo.Entry.EncryptedSize,
                        out padding);
                }

                fromStream = this.fromAdapter.GetReadStreamForEntry(this.updateInfo.Entry);
                toStream = this.toAdapter.GetWriteStreamForEntry(this.updateInfo.Entry, writeStreamLength);

                if (this.relationship.EncryptionMode != EncryptionMode.None)
                {
                    // Create a copy of the certificate from the original cert's handle. A unique copy is required
                    // because the encryption manager will dispose of the RSA CSP derived from the cert, and will 
                    // cause an ObjectDisposedException on the next file encryption
                    X509Certificate2 certificate = new X509Certificate2(this.encryptionCertificate.Handle);

                    this.encryptionManager = new EncryptionManager(
                        certificate, this.relationship.EncryptionMode,
                        toStream, this.updateInfo.Entry.GetSize(this.relationship, SyncEntryPropertyLocation.Source));
                }

                TransferResult result = await this.TransferDataWithTransformsAsync(fromStream, toStream)
                    .ConfigureAwait(false);

                if (this.relationship.EncryptionMode == EncryptionMode.Encrypt)
                {
                    // The file was encrytped, so we read the original file and wrote the encrypted file.
                    this.updateInfo.OriginalSizeNew = result.BytesRead;
                    this.updateInfo.EncryptedSizeNew = result.BytesWritten;

                    this.updateInfo.OriginalSha1HashNew = result.Sha1Hash;
                    this.updateInfo.OriginalMd5HashNew = result.Md5Hash;

                    this.updateInfo.EncryptedSha1HashNew = result.TransformedSha1Hash;
                    this.updateInfo.EncryptedMd5HashNew = result.TransformedMd5Hash;

                    // Add the hash information to the entry that was copied
                    this.updateInfo.Entry.OriginalSha1Hash = result.Sha1Hash;
                    this.updateInfo.Entry.EncryptedSha1Hash = result.TransformedSha1Hash;
                    this.updateInfo.Entry.OriginalMd5Hash = result.Md5Hash;
                    this.updateInfo.Entry.EncryptedMd5Hash = result.TransformedMd5Hash;
                }
                else if (this.relationship.EncryptionMode == EncryptionMode.Decrypt)
                {
                    // The file was descrypted, so we read the encrypted file and wrote the original (unencrypted) file.
                    this.updateInfo.EncryptedSizeNew = result.BytesRead;
                    this.updateInfo.OriginalSizeNew = result.BytesWritten;

                    this.updateInfo.EncryptedSha1HashNew = result.Sha1Hash;
                    this.updateInfo.EncryptedMd5HashNew = result.Md5Hash;

                    this.updateInfo.OriginalSha1HashNew = result.TransformedSha1Hash;
                    this.updateInfo.OriginalMd5HashNew = result.TransformedMd5Hash;

                    // Add the hash information to the entry that was copied
                    this.updateInfo.Entry.OriginalSha1Hash = result.TransformedSha1Hash;
                    this.updateInfo.Entry.EncryptedSha1Hash = result.Sha1Hash;
                    this.updateInfo.Entry.OriginalMd5Hash = result.TransformedMd5Hash;
                    this.updateInfo.Entry.EncryptedMd5Hash = result.Md5Hash;
                }
                else
                {
                    // The file was transferred without any encryption operation
                    this.updateInfo.OriginalSizeNew = result.BytesRead;

                    this.updateInfo.OriginalSha1HashNew = result.Sha1Hash;
                    this.updateInfo.OriginalMd5HashNew = result.Md5Hash;

                    this.updateInfo.Entry.OriginalSha1Hash = result.Sha1Hash;
                    this.updateInfo.Entry.OriginalMd5Hash = result.Md5Hash;
                }
            }
            finally
            {
                // Finalize the file transfer at the adapter-level. This method allows the adapter to call
                // any necessary method to complete the transfer. Note that this method MUST be called in the 
                // finally block (so that it is called even when the transfer throws an exception) and before 
                // disposing of the streams.
                this.toAdapter.FinalizeItemWrite(toStream, this.updateInfo);

                this.encryptionManager?.Dispose();

                fromStream?.Close();
                toStream?.Close();
            }
        }

        public class MyClass
        {
            public int MyVar;
        }

        /// <summary>
        /// Transfer data from the source stream to the destination stream. This method also performs the necessary
        /// data transforms (throttling, hashing, encrypting, etc) as the data is streamed. This allows a the 
        /// source stream to only be read a single time while performing multiple operations on the data.
        /// </summary>
        /// <param name="sourceStream">The stream that the data is read from</param>
        /// <param name="destinationStream">The stream that the data is written to</param>
        /// <returns>(async) The result of the transfer</returns>
        private async Task<TransferResult> TransferDataWithTransformsAsync(
            Stream sourceStream,
            Stream destinationStream)
        {
            SHA1Cng sha1 = null;
            MD5Cng md5 = null;

            try
            {
                // By default, we will compute the SHA1 and MD5 hashes of the file as it is streamed.
                sha1 = new SHA1Cng();
                md5 = new MD5Cng();

                // Allocate the buffer that will be used to transfer the data. The source stream will be read one
                // suffer-size as a time, then written to the destination.
                byte[] buffer = new byte[transferBufferSize];
                long readTotal = 0;
                long writtenTotal = 0;

                while (true)
                {
                    // If we are using a throttling manager, get the necessary number of tokens to
                    // transfer the data
                    if (this.throttlingManager != null)
                    {
                        int tokens = 0;
                        while (true)
                        {
                            // Get the number of tokens needed. We will require tokens equaling the number of
                            // bytes to be transferred. This is a non-blocking calling and will return between
                            // 0 and the number of requested tokens.
                            tokens += this.throttlingManager.GetTokens(transferBufferSize - tokens);
                            if (tokens >= transferBufferSize)
                            {
                                // We have enough tokens to transfer the buffer
                                break;
                            }

                            // We don't (yet) have enough tokens, so wait for a short duration and try again
                            await Task.Delay(10, this.cancellationToken).ConfigureAwait(false);
                        }
                    }

                    // Read data from the source adapter
                    int read = sourceStream.Read(buffer, 0, buffer.Length);

                    // Increment the total number of bytes read from the source adapter
                    readTotal += read;

                    if (read < buffer.Length)
                    {
                        // Compute the last part of the SHA1 and MD5 hashes (this finished the algorithm's work).
                        sha1.TransformFinalBlock(buffer, 0, read);
                        md5.TransformFinalBlock(buffer, 0, read);

                        if (this.encryptionManager != null)
                        {
                            writtenTotal += this.encryptionManager.TransformFinalBlock(buffer, 0, read);
                        }
                        else
                        {
                            destinationStream.Write(buffer, 0, read);
                            destinationStream.Flush();
                            writtenTotal += buffer.Length;
                        }

                        // Increment the total number of bytes written to the desination adapter
                        this.bytesCompleted += read;

                        this.progressChanged(new CopyProgressInfo(this.bytesCompleted, this.updateInfo));

                        // Read the end of the stream
                        break;
                    }

                    // Pass the data through the required hashing algorithms.
                    sha1.TransformBlock(buffer, 0, read, buffer, 0);
                    md5.TransformBlock(buffer, 0, read, buffer, 0);

                    // Write the data to the destination adapter
                    if (this.encryptionManager != null)
                    {
                        writtenTotal += this.encryptionManager.TransformBlock(buffer, 0, read);
                    }
                    else
                    {
                        destinationStream.Write(buffer, 0, read);
                        writtenTotal += buffer.Length;
                    }

                    // Increment the total number of bytes written to the desination adapter
                    this.bytesCompleted += read;

                    if (this.syncProgressUpdateStopwatch.ElapsedMilliseconds > 100)
                    {
                        this.progressChanged(new CopyProgressInfo(this.bytesCompleted, this.updateInfo));

                        // After reporting the number of bytes copied for this file, set back to 0 so that we are only
                        // reporting the number of bytes sync we last invoked the callback.
                        this.bytesCompleted = 0;

                        this.syncProgressUpdateStopwatch.Restart();
                    }
                }

                TransferResult result = new TransferResult
                {
                    BytesRead = readTotal,
                    BytesWritten = writtenTotal
                };

                if (this.encryptionManager != null)
                {
                    if (this.encryptionManager.Mode == EncryptionMode.Encrypt)
                    {
                        result.Sha1Hash = sha1.Hash;
                        result.Md5Hash = md5.Hash;
                        result.TransformedSha1Hash = this.encryptionManager.Sha1Hash;
                        result.TransformedMd5Hash = this.encryptionManager.Md5Hash;
                    }
                    else
                    {
                        result.TransformedSha1Hash = sha1.Hash;
                        result.TransformedMd5Hash = md5.Hash;
                        result.Sha1Hash = this.encryptionManager.Sha1Hash; // The SHA1 hash of the data written by the encryption manager
                        result.Md5Hash = this.encryptionManager.Md5Hash;
                    }
                }
                else
                {
                    result.Sha1Hash = sha1.Hash;
                    result.Md5Hash = md5.Hash;
                }

                return result;
            }
            finally
            {
                sha1.Dispose();
                md5.Dispose();
            }
        }
        private class TransferResult
        {
            public long BytesRead { get; set; }
            public long BytesWritten { get; set; }

            public byte[] Sha1Hash { get; set; }
            public byte[] Md5Hash { get; set; }

            public byte[] TransformedSha1Hash { get; set; }
            public byte[] TransformedMd5Hash { get; set; }
        }
    }


    public class RestoreOnlyWindowsFileSystemAdapter : AdapterBase
    {
        public static readonly Guid TargetTypeId = new Guid("a7e04307-efa5-43d9-8126-4ee0ed09171b");

        public RestoreOnlyWindowsFileSystemAdapterConfiguration Config =>
            (RestoreOnlyWindowsFileSystemAdapterConfiguration)this.Configuration;

        public RestoreOnlyWindowsFileSystemAdapter(
            SyncRelationship relationship,
            AdapterConfiguration configuration)
            : base(relationship, configuration)
        {
        }

        public override Guid GetTargetTypeId()
        {
            return TargetTypeId;
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
            if (entry.Type != SyncEntryType.File)
            {
                throw new InvalidOperationException("Cannot get a filestream for a non-file.");
            }

            string fullPath;
            using (var db = this.Relationship.GetDatabase())
            {
                fullPath = Path.Combine(this.Config.RootDirectory, entry.GetRelativePath(db, this.PathSeparator));
            }

            return File.Open(fullPath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
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

        public override void FinalizeItemWrite(Stream stream, EntryUpdateInfo updateInfo)
        {
            throw new NotImplementedException();
        }
    }

    public class RestoreOnlyWindowsFileSystemAdapterConfiguration : AdapterConfiguration
    {
        public override Guid AdapterTypeId => RestoreOnlyWindowsFileSystemAdapter.TargetTypeId;

        public string RootDirectory { get; set; }
    }
}