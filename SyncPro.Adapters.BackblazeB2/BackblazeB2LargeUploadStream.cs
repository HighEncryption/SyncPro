namespace SyncPro.Adapters.BackblazeB2
{
    using System;
    using System.IO;
    using System.Security.Cryptography;

    public class BackblazeB2LargeUploadStream : BufferedUploadStream
    {
        private readonly BackblazeB2Adapter adapter;

        internal BackblazeB2UploadSession Session { get; }

        public BackblazeB2LargeUploadStream(
            BackblazeB2Adapter adapter,
            BackblazeB2UploadSession session,
            long partSize,
            long fileSize)
            :base(partSize, fileSize)
        {
            this.adapter = adapter;
            this.Session = session;
        }

        protected override void UploadPart(byte[] partBuffer, long partOffset, long partIndex)
        {
            // A sha1 hash needs to be sent for each part that is uploaded. Because the part size is small (less
            // than 100MB) and the entire payload is loaded into memory (in the partBuffer array), computing the
            // hash should be quick operation (compared the to uploading delay).
            string sha1Hash;
            using (var sha1 = new SHA1Cng())
            {
                byte[] hashData = sha1.ComputeHash(partBuffer);
                sha1Hash = BitConverter.ToString(hashData).Replace("-", "").ToLowerInvariant();
            }

            using (MemoryStream memoryStream = new MemoryStream(partBuffer))
            {
                this.adapter.UploadPart(
                    this.Session,
                    this.Session.CurrentPartNumber,
                    sha1Hash,
                    partBuffer.LongLength,
                    memoryStream).Wait();

                this.Session.PartHashes.Add(
                    this.Session.CurrentPartNumber,
                    sha1Hash);

                this.Session.CurrentPartNumber++;

                this.Session.BytesUploaded += partBuffer.Length;
            }
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

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (count <= 0)
            {
                return;
            }

            Pre.ThrowIfArgumentNull(buffer, "buffer");
            Pre.ThrowIfTrue(buffer.Length == 0, "buffer.Length is 0");
            Pre.ThrowIfTrue(offset + count > buffer.Length, "offset + count > buffer.Length");

            base.Write(buffer, offset, count);
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
    }
}