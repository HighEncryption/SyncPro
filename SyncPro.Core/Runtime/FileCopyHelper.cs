namespace SyncPro.Runtime
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;

    using SyncPro.Adapters;
    using SyncPro.Configuration;
    using SyncPro.Data;

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
}