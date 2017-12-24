namespace SyncPro.Adapters.BackblazeB2
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    using SyncPro.Adapters.BackblazeB2.DataModel;
    using SyncPro.Data;

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

        //private void UploadComplete(Task<BackblazeB2FileUploadResponse> uploadTask)
        //{
        //    if (uploadTask.IsCanceled)
        //    {
        //        throw new UploadCancelledException();
        //    }

        //    if (uploadTask.IsFaulted)
        //    {
        //        throw new UploadException("An exception was thrown during the upload.", uploadTask.Exception);
        //    }

        //    BackblazeB2FileUploadResponse response = uploadTask.Result;

        //    SyncEntryAdapterData adapterData =
        //        this.Session.Entry.AdapterEntries.FirstOrDefault(a => a.AdapterId == this.adapter.Configuration.Id);

        //    if (adapterData == null)
        //    {
        //        adapterData = new SyncEntryAdapterData
        //        {
        //            SyncEntry = this.Session.Entry,
        //            AdapterId = this.adapter.Configuration.Id
        //        };
        //    }

        //    adapterData.AdapterEntryId = response.FileId;
        //}

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
                
            }

            base.Dispose(disposing);
        }
    }
}