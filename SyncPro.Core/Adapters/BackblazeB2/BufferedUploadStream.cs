namespace SyncPro.Adapters.BackblazeB2
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    public abstract class BufferedUploadStream : Stream
    {
        // The local list of buffers where data written to the stream is saved until enough data has accumulated.
        private readonly List<byte[]> buffers = new List<byte[]>();

        // The total size (length) of all buffers
        private long totalSize;

        // The number of bytes remaining to be sent
        private long bytesRemaining;

        // The upload part offset. Each part uploaded will increment this value by 1.
        private long partOffset;

        // The size of the part to upload. Must be a multiple of 320KiB per the OneDrive documentation.
        private readonly long partSize;

        protected BufferedUploadStream(long partSize, long totalSize)
        {
            Pre.Assert(partSize > 0, "partSize > 0");
            Pre.Assert(totalSize > 0, "totalSize > 0");
            Pre.Assert(totalSize >= partSize, "totalSize >= partSize");

            this.partSize = partSize;
            this.totalSize = totalSize;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            // Allocate a new buffer locally (since the buffer provided by the caller might not exist after the call 
            // returns) and copy the given buffer into the local buffer.
            byte[] localBuffer = new byte[count];
            Buffer.BlockCopy(buffer, offset, localBuffer, 0, count);

            // Add the new buffer to the list of buffers and update the total size.
            this.buffers.Add(localBuffer);
            this.totalSize += count;

            // If the total size of the buffers is at least the part size, flush the data (sending it to OneDrive).
            if (this.ArePartsAvailable())
            {
                this.Flush();
            }
        }

        public sealed override void Flush()
        {
            while (this.ArePartsAvailable())
            {
                // Coalase the buffers until we have a single buffer of the right size
                byte[] partBuffer = this.AccumulateBuffers();

                // Call the specific method for uploading the part
                this.UploadPart(partBuffer, this.partOffset);

                this.partOffset += this.partSize;
                this.bytesRemaining -= partBuffer.Length;
            }
        }

        protected abstract void UploadPart(byte[] partBuffer, long partOffset);

        protected bool ArePartsAvailable()
        {
            // Send parts as long as either:
            //   a) there is at least a part's worth of data in the buffers, or
            //   b) the buffers contain all of the final part
            return this.totalSize >= this.partSize || (this.totalSize > 0 && this.totalSize == this.bytesRemaining);
        }

        /// <summary>
        /// Combine existing buffers into a single buffer equal to the part size or less.
        /// </summary>
        private byte[] AccumulateBuffers()
        {
            // If the total about of data in the buffers is less than the part size, only allocate a
            // buffer of that size.
            long partBufferSize = this.totalSize < this.partSize ? this.totalSize : this.partSize;

            // Allocate the buffer that will hold the part to be returned
            byte[] partBuffer = new byte[partBufferSize];

            // Index of the current buffer that is being read
            int listIndex = 0;

            // Offset within the current buffer being read
            int listOffset = 0;

            // Loop over the part buffer, copying in bytes one at a time from the source buffers
            for (int i = 0; i < partBufferSize; i++)
            {
                partBuffer[i] = this.buffers[listIndex][listOffset];
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
            this.totalSize -= partBufferSize;
            return partBuffer;
        }
    }
}