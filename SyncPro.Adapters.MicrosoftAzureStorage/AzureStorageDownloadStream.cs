namespace SyncPro.Adapters.MicrosoftAzureStorage
{
    using System;
    using System.IO;
    using System.Net.Http;

    public class AzureStorageDownloadStream : Stream
    {
        private readonly AzureStorageClient client;
        private readonly string containerName;
        private readonly string fileName;
        private readonly long fileSize;

        // The default fragment size to download is 10MB.
        // Developer Note: There is a tradeoff to be made here. A larger fragment size
        // will reduce the out-of-band data and would result in fewer overall requests
        // send to Azure. However, because we download files in parallel, larger
        // fragments would require larger buffer, increasing memory usage during a read
        // of a file. 10MB seems like a good choice (2018/10/12).
        private const int FragmentLength = 0xA00000;

        // The buffer where we will hold data read from Azure while we are waiting for
        // the caller to finish reading all of it.
        private byte[] downloadBuffer = new byte[0];

        private int downloadBufferOffset;
        private int fragmentOffset;
        private bool isFinalBuffer;

        public AzureStorageDownloadStream(
            AzureStorageClient client, 
            string containerName,
            string fileName, 
            long fileSize)
        {
            this.client = client;
            this.containerName = containerName;
            this.fileName = fileName;
            this.fileSize = fileSize;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            for (int i = 0; i < count; i++)
            {
                // Check if we need to download a fragment
                if (this.downloadBufferOffset >= this.downloadBuffer.Length)
                {
                    if (this.isFinalBuffer)
                    {
                        return i;
                    }

                    this.ReadNextFragment();
                }

                buffer[i + offset] = this.downloadBuffer[this.downloadBufferOffset];

                this.downloadBufferOffset++;
            }

            return count;
        }

        private void ReadNextFragment()
        {
            long rangeStart = this.fragmentOffset * FragmentLength;
            long rangeEnd = FragmentLength;

            if (rangeStart + FragmentLength > this.fileSize)
            {
                rangeEnd = this.fileSize - 1;
            }

            HttpResponseMessage response;
            if (rangeStart == 0 && rangeEnd == this.fileSize - 1)
            {
                response = this.client.GetBlobAsync(
                    this.containerName, 
                    this.fileName).Result;

                this.downloadBuffer = response.Content.ReadAsByteArrayAsync().Result;
                this.isFinalBuffer = true;
                return;
            }

            response = this.client.GetBlobRangeAsync(
                this.containerName, 
                this.fileName,
                rangeStart, 
                rangeEnd).Result;

            using (response)
            {
                this.downloadBuffer = response.Content.ReadAsByteArrayAsync().Result;

                this.fragmentOffset++;
                this.downloadBufferOffset = 0;

                var rangeHeader = response.Content.Headers.ContentRange;
                Pre.Assert(rangeHeader.To != null, "rangeHeader.To != null");
                Pre.Assert(rangeHeader.Length != null, "rangeHeader.Length != null");

                if (rangeHeader.To.Value == rangeHeader.Length - 1)
                {
                    this.isFinalBuffer = true;
                }
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
    }
}