namespace SyncPro.Adapters.MicrosoftAzureStorage
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Security.Cryptography;

    public class AzureStorageUploadStream : BufferedUploadStream
    {
        // The default fragment size to upload is 10MB.
        // Developer Note: There is a tradeoff to be made here. A larger fragment size
        // will reduce the out-of-band data and would result in fewer overall requests
        // send to Azure. However, because we upload files in parallel, larger
        // fragments would require larger buffer, increasing memory usage during a write
        // of a file. 10MB seems like a good choice (2018/10/12).
        private const int FragmentLength = 0xA00000;

        private readonly AzureStorageClient client;
        private readonly string containerName;
        private readonly long fileSize;

        internal string FileName { get; }

        internal List<string> BlockList { get; } = new List<string>();

        public AzureStorageUploadStream(
            AzureStorageClient client,
            string containerName,
            string fileName,
            long fileSize)
            : base(FragmentLength,
                fileSize)
        {
            this.client = client;
            this.containerName = containerName;
            this.FileName = fileName;
            this.fileSize = fileSize;
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        protected override void UploadPart(byte[] partBuffer, long partOffset, long partIndex)
        {
            byte[] hashBytes;
            HttpResponseMessage result;
            string blockId = null;

            using (MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider())
            {
                hashBytes = md5.ComputeHash(partBuffer);
            }

            // If we receive a single buffer that is the entire size of the file, then we do not 
            // need to upload the file in blocks, and can instead just put the blob with a single
            // request.
            if (partBuffer.Length == this.fileSize)
            {
                result = this.client.PutBlobAsync(
                    this.containerName,
                    this.FileName,
                    partBuffer,
                    hashBytes).Result;

                if (!result.IsSuccessStatusCode)
                {
                    throw new AzureStorageHttpException();
                }

                return;
            }

            string hash = BitConverter.ToString(hashBytes).Replace("-", "");
            blockId = string.Format("{0:d5}_{1}", partIndex, hash);

            result = this.client.PutBlockAsync(
                this.containerName,
                this.FileName,
                blockId,
                partBuffer,
                hashBytes).Result;

            if (!result.IsSuccessStatusCode)
            {
                throw new AzureStorageHttpException();
            }

            if (!string.IsNullOrWhiteSpace(blockId))
            {
                this.BlockList.Add(blockId);
            }
        }
    }
}