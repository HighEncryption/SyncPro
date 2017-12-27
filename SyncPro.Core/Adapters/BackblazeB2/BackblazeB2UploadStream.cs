namespace SyncPro.Adapters.BackblazeB2
{
    using System;
    using System.IO;

    public class BackblazeB2UploadStream : Stream
    {
        private readonly MemoryStream memoryStream;

        private readonly BackblazeB2Adapter adapter;

        internal BackblazeB2UploadSession Session { get; }

        public BackblazeB2UploadStream(
            BackblazeB2Adapter adapter,
            BackblazeB2UploadSession session)
        {
            this.adapter = adapter;
            this.Session = session;

            this.memoryStream = new MemoryStream();
        }

        public override void Flush()
        {
            if (this.memoryStream.Length < this.Session.Entry.Size)
            {
                return;
            }

            this.Session.UploadResponse = 
                this.adapter.UploadFileDirect(this.Session.Entry, this.memoryStream).Result;
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

            this.memoryStream.Write(buffer, offset, count);
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.memoryStream?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}