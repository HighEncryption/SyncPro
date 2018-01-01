namespace SyncPro.Runtime
{
    using System;
    using System.IO;

    // The encrypted file format is as follows:
    // +-----------------------------------------+   -\
    // |      encryptionKey.Length (4 bytes)     |    |
    // +-----------------------------------------+    |
    // |        encryptionKey (512 bytes)        |    |
    // +-----------------------------------------+    |
    // |  initializationVector.Length (4 bytes)  |    |
    // +-----------------------------------------+    |  (Header)
    // |     initializationVector (16 bytes)     |    |
    // +-----------------------------------------+    |
    // |      originalFile.Length (8 bytes)      |    |
    // +-----------------------------------------+    |
    // |      encryptedFile.Length (8 bytes)     |    |
    // +-----------------------------------------+    |
    // |         padding.Length (2 bytes)        |    |
    // +-----------------------------------------+    |
    // |            reserved (6 bytes)           |    |
    // +-----------------------------------------+   -/
    // |             encryptedData               |
    // +-----------------------------------------+
    // |                padding                  |
    // +-----------------------------------------+
    //
    // The header block is comprised of the first 6 fields, and will always have the same size. The
    // encryptedData field will be equal to (plainText + blockSize - (plainText MOD blockSize). The padding
    // is appended to the end of the file, and contains only nulls.
    //
    // The purpose of including the padding at the end is that it allows the unencrypted size of the file to
    // be calculated without reading the header block. This is important becase we will need to create the
    // destination (decrypted) file, which sometimes requires that we specify the length of the file when
    // creating it.
    //
    // The padding field is included so that we can verify that the original file length is correct.
    public class EncryptedFileHeader
    {
        /// <summary>
        /// The size of the space allocated (in bytes) for the encrypted key stored at the beginning of the 
        /// encrypted file
        /// </summary>
        public const int EncryptedKeyStorageSize = 512;

        /// <summary>
        /// The size of the space allocated (in bytes) of the initialization vector (IV) stored at the beginning of 
        /// the encrypted file
        /// </summary>
        public const int IVStorageSize = 16;

        public const int HeaderReservedSize = 6;

        public const int Size =
            sizeof(int) +
            EncryptedKeyStorageSize +
            sizeof(int) +
            IVStorageSize +
            sizeof(long) +
            sizeof(long) +
            sizeof(short) +
            HeaderReservedSize;

        public byte[] EncryptedKey { get; set; }

        public byte[] IV { get; set; }

        public long OriginalFileLength { get; set; }

        public long EncryptedFileLength { get; set; }

        public short PaddingLength { get; set; }

        /// <summary>
        /// Read the encrypted file header information from a stream
        /// </summary>
        /// <param name="stream">The stream to read</param>
        /// <returns>The encrypted header object</returns>
        public static EncryptedFileHeader ReadFromStream(Stream stream)
        {
            Pre.ThrowIfArgumentNull(stream, nameof(stream));

            EncryptedFileHeader header = new EncryptedFileHeader();

            int encryptedKeyLength = stream.ReadInt32();

            // Ensure the key is not longer than the buffer that is supposed to contain it
            Pre.Assert(
                encryptedKeyLength <= EncryptedKeyStorageSize,
                "encryptedKeyLength <= EncryptedKeyStorageSize");

            // Read the full encrypted key field. This will ensure that the stream's internal read
            // location is advanced completely.
            byte[] keyEncryptedRaw = stream.ReadByteArray(EncryptedKeyStorageSize);

            // Copy the encrypted key to the field in the header object
            header.EncryptedKey = new byte[encryptedKeyLength];
            Buffer.BlockCopy(keyEncryptedRaw, 0, header.EncryptedKey, 0, encryptedKeyLength);

            // Read the IV length. This should be 16 bytes (asserted below).
            int ivLength = stream.ReadInt32();

            Pre.Assert(
                ivLength == IVStorageSize,
                "ivLength == IVStorageSize");

            // Read the initialization vector
            header.IV = stream.ReadByteArray(ivLength);

            // Read the original file size and padding
            header.OriginalFileLength = stream.ReadInt64();
            header.EncryptedFileLength = stream.ReadInt64();
            header.PaddingLength = stream.ReadInt16();

            // Read the reserved bytes
            stream.ReadByteArray(6);

            return header;
        }

        public void WriteToStream(MemoryStream bufferedOutputStream)
        {
            Pre.Assert(this.EncryptedKey != null, "this.EncryptedKey != null");
            Pre.Assert(this.EncryptedKey.Length > 0, "this.EncryptedKey.Length > 0");

            Pre.Assert(this.IV != null, "this.IV != null");
            Pre.Assert(this.IV.Length > 0, "this.IV.Length > 0");

            Pre.Assert(this.OriginalFileLength > 0, "this.OriginalFileLength > 0");
            Pre.Assert(this.EncryptedFileLength > 0, "this.EncryptedFileLength > 0");

            // Create the full-length buffer where the key will be stored. This is the complete byte array
            // that will be written to to file header. It is declared separately to ensure that the length
            // of the array is correct.
            byte[] keyEncryptedBuffer = new byte[EncryptedKeyStorageSize];

            // Copy the encrypted key into the header buffer
            Buffer.BlockCopy(this.EncryptedKey, 0, keyEncryptedBuffer, 0, this.EncryptedKey.Length);

            // Write the encrypted key length and key into the temporary buffer
            bufferedOutputStream.WriteInt32(this.EncryptedKey.Length);
            bufferedOutputStream.Write(keyEncryptedBuffer, 0, EncryptedKeyStorageSize);

            // Write the initialization vector and length to the temporary buffer
            bufferedOutputStream.WriteInt32(this.IV.Length);
            bufferedOutputStream.Write(this.IV, 0, this.IV.Length);

            // Write the original file file and encrypted file size
            bufferedOutputStream.WriteInt64(this.OriginalFileLength);
            bufferedOutputStream.WriteInt64(this.EncryptedFileLength);

            // Write the padding length and the reserved bytes
            bufferedOutputStream.WriteInt16(this.PaddingLength);
            bufferedOutputStream.Write(new byte[HeaderReservedSize], 0, HeaderReservedSize);
        }
    }
}