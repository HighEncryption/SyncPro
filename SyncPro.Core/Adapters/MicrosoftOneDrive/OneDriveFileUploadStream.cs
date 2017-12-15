namespace SyncPro.Adapters.MicrosoftOneDrive
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using SyncPro.Tracing;

    /// <summary>
    /// Provides a Stream implementation for writing a file to OneDrive storage.
    /// </summary>
    /// <remarks>
    /// REST implementation: https://dev.onedrive.com/items/upload_large_files.htm
    /// This class exposes a file upload session to OneDrive as a write-only stream. This allows methods that operate on
    /// streams to write files directly to OneDrive. As data is written to the stream, it is cached in buffers local to
    /// the stream until a threshold is met. One enough data has been accumulated in buffers, the data is sent syncronously
    /// to OneDrive. The Write() method call will block until the write completes.
    /// </remarks>
    public class OneDriveFileUploadStream : Stream
    {
        // The smallest allowed fragment size (320 KiB). Also, fragment sizes must be a multiple of this value.
        private const int fragmentSizeBase = 327680;

        // The client used to write the data to OneDrive
        private readonly OneDriveClient client;

        // The upload session containing the upload Url where the data is sent along with metadata about the upload
        private readonly OneDriveUploadSession uploadSession;

        // The local list of buffers where data written to the stream is saved until enough data has accumulated to send
        // to OneDrive. Each Write() call will result in the creation of a new buffer, so larger writes are preferred over 
        // small write. 
        private readonly List<byte[]> buffers = new List<byte[]>();

        // The total size (length) of all buffers
        private long totalSize;

        // The number of bytes remaining to be sent
        private long bytesRemaining;

        // The upload fragment offset. Each fragment sent to OneDrive will increment this value by 1.
        private long fragmentOffset;

        // The size of the fragment to upload. Must be a multiple of 320KiB per the OneDrive documentation.
        private readonly int fragmentSize;

        /// <summary>
        /// The default fragement size (10 MiB), used for high-speed connection.
        /// </summary>
        public const int DefaultFragmentSize = 10485760;

        /// <summary>
        /// The recommended small fragment size (5 MiB) , used for connections that are slower or less reliable.
        /// </summary>
        public const int RecommendedSmallFragmentSize = 5242880;

        internal OneDriveFileUploadStream(OneDriveClient client, OneDriveUploadSession uploadSession)
            : this(client, uploadSession, DefaultFragmentSize)
        {
        }

        internal OneDriveFileUploadStream(OneDriveClient client, OneDriveUploadSession uploadSession, int fragmentSize)
        {
            Pre.ThrowIfArgumentNull(client, nameof(client));
            Pre.ThrowIfArgumentNull(uploadSession, nameof(uploadSession));

            if (fragmentSize < fragmentSizeBase)
            {
                throw new ArgumentOutOfRangeException("The fragement size must be at least " + fragmentSizeBase);
            }

            if (fragmentSize % fragmentSizeBase != 0)
            {
                throw new ArgumentException("The segement size must be a multiple of " + fragmentSizeBase,
                    nameof(fragmentSize));
            }

            this.client = client;
            this.uploadSession = uploadSession;
            this.fragmentSize = fragmentSize;
            this.bytesRemaining = uploadSession.Length;
        }

        private bool AreFragmentAvailable()
        {
            // Send fragments as long as either:
            //   a) there is at least a fragment's worth of data in the buffers, or
            //   b) the buffers contain all of the final fragment
            return this.totalSize >= this.fragmentSize || (this.totalSize > 0 && this.totalSize == this.bytesRemaining);
        }

        public override void Flush()
        {
            while (this.AreFragmentAvailable())
            {
                // Coalase the buffers until we have a single buffer of the right size
                byte[] fragmentBuffer = this.AccumulateBuffers();

                // Upload the fragment to OneDrive
                this.client.SendUploadFragment(this.uploadSession, fragmentBuffer, this.fragmentOffset).Wait();

                this.fragmentOffset += this.fragmentSize;
                this.bytesRemaining -= fragmentBuffer.Length;
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (count <= 0)
            {
                return;
            }

            Pre.ThrowIfArgumentNull(buffer, "buffer");
            Pre.ThrowIfTrue(buffer.Length == 0, "buffer.Length is 0");
            Pre.ThrowIfTrue(offset + count > buffer.Length, "offset + count > buffer.Length");

            switch (this.uploadSession.State)
            {
                case OneDriveFileUploadState.Completed:
                    throw new OneDriveException("Cannot write to completed upload session.");
                case OneDriveFileUploadState.Faulted:
                    throw new OneDriveException("Cannot write to faulted upload session.");
                case OneDriveFileUploadState.Cancelled:
                    throw new OneDriveException("Cannot write to cancelled upload session.");
            }

            // Check if the new data will be more than the file size specified in the session
            if (this.totalSize + count > this.uploadSession.Length)
            {
                Logger.Error(
                    "OneDrive file upload overflow. File={0}, ParentId={1}, Length={2}, CurrentSize={3}, WriteSize={4}",
                    this.uploadSession.ItemName,
                    this.uploadSession.ParentId,
                    this.uploadSession.Length,
                    this.totalSize,
                    count);

                throw new OneDriveException("More data was written to the stream than is allowed by the file.");
            }

            // Allocate a new buffer locally (since the buffer provided by the caller might not exist after the call 
            // returns) and copy the given buffer into the local buffer.
            byte[] localBuffer = new byte[count];
            Buffer.BlockCopy(buffer, offset, localBuffer, 0, count);

            // Add the new buffer to the list of buffers and update the total size.
            this.buffers.Add(localBuffer);
            this.totalSize += count;

            // If the total size of the buffers is at least the fragment size, flush the data (sending it to OneDrive).
            if (this.AreFragmentAvailable())
            {
                this.Flush();
            }
        }

        /// <summary>
        /// Combine existing buffers into a single buffer equal to the fragment size or less.
        /// </summary>
        private byte[] AccumulateBuffers()
        {
            // If the total about of data in the buffers is less than the fragment size, only allocate a
            // buffer of that size.
            long fragmentBufferSize = this.totalSize < this.fragmentSize ? this.totalSize : this.fragmentSize;

            // Allocate the buffer that will hold the fragment to be returned
            byte[] fragmentBuffer = new byte[fragmentBufferSize];

            // Index of the current buffer that is being read
            int listIndex = 0;

            // Offset within the current buffer being read
            int listOffset = 0;

            // Loop over the fragment buffer, copying in bytes one at a time from the source buffers
            for (int i = 0; i < fragmentBufferSize; i++)
            {
                fragmentBuffer[i] = this.buffers[listIndex][listOffset];
                listOffset++;

                // If we pass the end of the current buffer, move to the next buffer
                if (listOffset >= this.buffers[listIndex].Length)
                {
                    // If we have read all of the buffers, break from the loop
                    if (listIndex >= this.buffers.Count)
                    {
                        break;
                    }

                    // There are more buffers to read, so move to the next buffer and reset the index
                    listIndex++;
                    listOffset = 0;
                }
            }

            // Resize the last list we were on (if needed)
            if (listOffset > 0)
            {
                byte[] buf = this.buffers[listIndex];
                Array.Resize(ref buf, buf.Length - listOffset);
                this.buffers[listIndex] = buf;
            }

            // Remove any buffers from the buffer list that were emptied as a result
            for (int i = 0; i < listIndex && this.buffers.Any(); i++)
            {
                this.buffers.RemoveAt(0);
            }

            // Update totalSize and return the new buffer
            this.totalSize -= fragmentBufferSize;
            return fragmentBuffer;
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length
        {
            get {  throw new NotSupportedException(); }
        }

        public override long Position
        {
            get { throw new NotSupportedException(); }
            set {  throw new NotSupportedException(); }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new InvalidOperationException();
        }

        public override void SetLength(long value)
        {
            throw new InvalidOperationException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // If the upload session was uploaded successfully or has already been cancelled, then
                // nothing needs to be done in dispose.
                if (this.uploadSession.State == OneDriveFileUploadState.Completed ||
                    this.uploadSession.State == OneDriveFileUploadState.Cancelled)
                {
                    return;
                }

                // If the upload is not faulted, call flush to ensure that any remaining data has been written to OneDrive
                if (this.uploadSession.State != OneDriveFileUploadState.Faulted)
                {
                    this.Flush();

                    // If the flush succeeded and the upload is complete, return since we do not want to cancel the upload
                    if (this.uploadSession.State == OneDriveFileUploadState.Completed)
                    {
                        return;
                    }
                }

                Logger.Warning("Cancelling incomplete upload");

                // If all of the required data has not been written to the stream, delete the upload session in OneDrive
                // and set the upload session as failed.
                try
                {
                    this.client.CancelUploadSession(this.uploadSession).Wait();
                }
                catch (Exception exception)
                {
                    Logger.Info("Suppressing exception from CancelUploadSession(): " + exception.Message);
                }

                if (this.uploadSession.State == OneDriveFileUploadState.Cancelled)
                {
                    return;
                }

                Logger.Warning("Upload cancellation failed");
            }

            base.Dispose(disposing);
        }
    }
}