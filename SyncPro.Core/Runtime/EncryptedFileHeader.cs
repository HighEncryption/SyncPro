namespace SyncPro.Runtime
{
    using System;
    using System.IO;
    using System.Security.Cryptography;

    // The encrypted file format is as follows:
    // +----------------------------------------+   -\
    // |     encryptionKey.Length (4 bytes)     |    |
    // +----------------------------------------+    |
    // |        encryptionKey (variable)        |    |
    // +----------------------------------------+    |
    // |  initializationVectorLength (4 bytes)  |    |
    // +----------------------------------------+    |
    // |     initializationVector (16 bytes)    |    |
    // +----------------------------------------+    |
    // |      originalFileLength (8 bytes)      |    |
    // +----------------------------------------+    |  Header (1024 bytes)
    // |      encryptedFileLength (8 bytes)     |    |
    // +----------------------------------------+    |
    // |    certificateThumbprint (20 bytes)    |    |
    // +----------------------------------------+    |
    // |         paddingLength (2 bytes)        |    |
    // +----------------------------------------+    |
    // |           reserved (variable)          |    |
    // +----------------------------------------+    |
    // |          headerSha1 (20 bytes)         |    |
    // +----------------------------------------+   -/
    // |             encryptedData              |
    // +----------------------------------------+
    // |                padding                 |
    // +----------------------------------------+
    //
    // The header block is statically defined as being the first 1k of the encrypted file. Fields are written
    // to the header block starting at the beginning, except for the header SHA1 (checksum) which is written 
    // to the last 20 bytes of the header.
    // The encryptedData field will be equal to (plainText + blockSize - (plainText MOD blockSize).
    // The padding is appended to the end of the file, and contains only nulls.
    //
    // The purpose of including the padding at the end is that it allows the unencrypted size of the file to
    // be calculated without reading the header block. This is important becase we will need to create the
    // destination (decrypted) file, which sometimes requires that we specify the length of the file when
    // creating it.
    public class EncryptedFileHeader
    {
        public const int HeaderSize = 1024;

        public const int ChecksumSize = 20;

        public const int CertificateThumbprintSize = 20;

        public const int EncryptedKeyMaxLength = 512;

        public const int IVStorageSize = 16;

        public byte[] EncryptedKey { get; set; }

        public byte[] IV { get; set; }

        public long OriginalFileLength { get; set; }

        public long EncryptedFileLength { get; set; }

        public byte[] CertificateThumbprint { get; set; }

        public short PaddingLength { get; set; }

        /// <summary>
        /// Read the encrypted file header information from a stream
        /// </summary>
        /// <param name="stream">The stream to read</param>
        /// <returns>The encrypted header object</returns>
        public static EncryptedFileHeader ReadFromStream(Stream stream)
        {
            Pre.ThrowIfArgumentNull(stream, nameof(stream));

            // Read the entire header from the stream
            byte[] buffer = stream.ReadByteArray(HeaderSize);

            // Verify the header's checksum
            using (SHA1CryptoServiceProvider sha1 = new SHA1CryptoServiceProvider())
            {
                byte[] computedHash = sha1.ComputeHash(buffer, 0, HeaderSize - ChecksumSize);
                byte[] headerHash = BufferUtil.CopyBytes(buffer, buffer.Length - ChecksumSize, ChecksumSize);

                if (!NativeMethods.ByteArrayEquals(computedHash, headerHash))
                {
                    // TODO: Replace with a better exception
                    throw new Exception("The header checksum failed");
                }
            }

            // Create a new stream for reading the header data. This will make it easier to process the 
            // fields in the header and will avoid having to reposition the original stream.
            using (MemoryStream headerStream = new MemoryStream(buffer))
            {
                EncryptedFileHeader header = new EncryptedFileHeader();

                int encryptedKeyLength = headerStream.ReadInt32();

                // Ensure the key is not longer than the buffer
                Pre.Assert(encryptedKeyLength < HeaderSize, "encryptedKeyLength < HeaderSize");

                // Read the encrypted key field. 
                header.EncryptedKey = headerStream.ReadByteArray(encryptedKeyLength);

                // Read the IV length. This should be 16 bytes (asserted below).
                int ivLength = headerStream.ReadInt32();

                Pre.Assert(ivLength == IVStorageSize, "ivLength == ivStorageSize");

                // Read the initialization vector
                header.IV = headerStream.ReadByteArray(ivLength);

                // Read the file sizes, thumbprint, and padding
                header.OriginalFileLength = headerStream.ReadInt64();
                header.EncryptedFileLength = headerStream.ReadInt64();
                header.CertificateThumbprint = headerStream.ReadByteArray(CertificateThumbprintSize);
                header.PaddingLength = headerStream.ReadInt16();

                return header;
            }
        }

        public void WriteToStream(MemoryStream bufferedOutputStream)
        {
            Pre.Assert(this.EncryptedKey != null, "this.EncryptedKey != null");
            Pre.Assert(this.EncryptedKey.Length > 0, "this.EncryptedKey.Length > 0");
            Pre.Assert(
                this.EncryptedKey.Length < EncryptedKeyMaxLength, 
                "this.EncryptedKey.Length < EncryptedKeyMaxLength");

            Pre.Assert(this.IV != null, "this.IV != null");
            Pre.Assert(this.IV.Length == IVStorageSize, "this.IV.Length == IVStorageSize");

            Pre.Assert(this.EncryptedFileLength > 0, "this.EncryptedFileLength > 0");

            byte[] headerBytes;

            // Create a temporary stream for writing the header
            using (MemoryStream stream = new MemoryStream())
            {
                // Write the encrypted key length and key
                stream.WriteInt32(this.EncryptedKey.Length);
                stream.Write(this.EncryptedKey, 0, this.EncryptedKey.Length);

                // Write the initialization vector and length
                stream.WriteInt32(this.IV.Length);
                stream.Write(this.IV, 0, this.IV.Length);

                // Write the original file file, encrypted file size, and thumbprint
                stream.WriteInt64(this.OriginalFileLength);
                stream.WriteInt64(this.EncryptedFileLength);
                stream.Write(this.CertificateThumbprint, 0, this.CertificateThumbprint.Length);

                // Write the padding length and the reserved bytes
                stream.WriteInt16(this.PaddingLength);

                headerBytes = stream.ToArray();
            }

            // Allocate the 1k buffer for the header. This will be written to the output stream after 
            // populating the fields.
            byte[] buffer = new byte[HeaderSize];

            Buffer.BlockCopy(headerBytes, 0, buffer, 0, headerBytes.Length);

            using (SHA1CryptoServiceProvider sha1 = new SHA1CryptoServiceProvider())
            {
                byte[] hash = sha1.ComputeHash(buffer, 0, HeaderSize - ChecksumSize);
                Buffer.BlockCopy(hash, 0, buffer, buffer.Length - ChecksumSize, ChecksumSize);
            }

            Buffer.BlockCopy(headerBytes, 0, buffer, 0, headerBytes.Length);

            bufferedOutputStream.Write(buffer, 0, buffer.Length);
        }
    }
}