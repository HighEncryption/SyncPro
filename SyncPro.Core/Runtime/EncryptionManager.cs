namespace SyncPro.Runtime
{
    using System;
    using System.Data.Entity.Infrastructure;
    using System.IO;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;

    public enum EncryptionMode
    {
        Encrypt,
        Decrypt
    }

    public class EncryptionManager : IDisposable
    {
        /// <summary>
        /// The default size (in bits) of the RSA key
        /// </summary>
        public const int DefaultCspKeySize = 256;

        /// <summary>
        /// The default size (in bits) of the RSA encryption block size
        /// </summary>
        public const int DefaultCspBlockSize = 128;

        /// <summary>
        /// The size of the space allocated (in bytes) for the encrypted key stored at the beginning of the 
        /// encrypted file
        /// </summary>
        public const int DefaultEncryptedKeyStorageSize = 512;

        /// <summary>
        /// The size of the space allocated (in bytes) of the initialization vector (IV) stored at the beginning of 
        /// the encrypted file
        /// </summary>
        public const int DefaultIVStorageSize = 16;

        private readonly X509Certificate2 encryptionCertificate;
        private readonly long sourceFileSize;

        private readonly SHA1Cng sha1;
        private readonly MD5Cng md5;

        private readonly Stream outputStream;

        private bool firstBlockTransformed;

        private RSACryptoServiceProvider rsa;
        private AesCryptoServiceProvider aes;
        private ICryptoTransform cryptoTransform;

        public EncryptionMode Mode { get; }

        public byte[] Sha1Hash => this.sha1.Hash;

        public byte[] Md5Hash => this.md5.Hash;

        public EncryptionManager(
            X509Certificate2 encryptionCertificate, 
            EncryptionMode mode,
            Stream outputStream,
            long sourceFileSize)
        {
            Pre.ThrowIfArgumentNull(encryptionCertificate, nameof(encryptionCertificate));
            Pre.ThrowIfArgumentNull(outputStream, nameof(outputStream));

            this.encryptionCertificate = encryptionCertificate;
            this.Mode = mode;
            this.sourceFileSize = sourceFileSize;
            this.outputStream = outputStream;

            this.sha1 = new SHA1Cng();
            this.md5 = new MD5Cng();
        }

        public void Initialize()
        {
            // Create the CSP. By default, this will generate a new Key and IV, so we need to ensure that we 
            // initialize for each file that is being encrypted. Each file should have a unique key and IV.
            this.aes = new AesCryptoServiceProvider
            {
                // Ensure that we are using the correct key size and block size
                KeySize = DefaultCspKeySize,
                BlockSize = DefaultCspBlockSize,

                // Ensure that we are using Cipher Block Chaining (CBC)
                Mode = CipherMode.CBC
            };
            
            if (this.Mode == EncryptionMode.Encrypt)
            {
                // We are encrypting the stream, so use the certificate public key as the CSP
                this.rsa = this.encryptionCertificate.PublicKey.Key as RSACryptoServiceProvider;
                Pre.Assert(this.rsa != null, "this.rsa != null");
            }
            else
            {
                // Initialize the CSP from the private key of the certificate.
                this.rsa = this.encryptionCertificate.PrivateKey as RSACryptoServiceProvider;
                Pre.Assert(this.rsa != null, "this.rsa != null");
            }
        }

        public int TransformBlock(byte[] buffer, int offset, int count)
        {
            using (MemoryStream bufferedOutputStream = new MemoryStream())
            {
                if (this.Mode == EncryptionMode.Encrypt)
                {
                    this.WriteEncrypted(bufferedOutputStream, buffer, offset, count);
                }
                else
                {
                    this.ReadEncrypted(bufferedOutputStream, buffer, offset, count);
                }

                byte[] transformedBuffer = bufferedOutputStream.ToArray();

                this.sha1.TransformBlock(transformedBuffer, 0, transformedBuffer.Length, null, 0);
                this.md5.TransformBlock(transformedBuffer, 0, transformedBuffer.Length, null, 0);

                return transformedBuffer.Length;
            }
        }

        public int TransformFinalBlock(byte[] inputBuffer, int offset, int count)
        {
            byte[] outputBuffer = new byte[0];
            int outputOffset = 0;

            if (!this.firstBlockTransformed)
            {
                if (this.Mode == EncryptionMode.Encrypt)
                {
                    using (MemoryStream bufferedOutputStream = new MemoryStream())
                    {
                        this.WriteFirstEncryptedBlock(bufferedOutputStream);
                        outputBuffer = bufferedOutputStream.ToArray();
                        outputOffset = outputBuffer.Length;
                    }
                }
                else
                {
                    this.ReadFirstEncryptedBlock(inputBuffer, ref offset, ref count);
                }
            }

            // Encrypt/decrypt the final block
            byte[] finalBlock = this.cryptoTransform.TransformFinalBlock(inputBuffer, offset, count);

            Array.Resize(ref outputBuffer, outputBuffer.Length + finalBlock.Length);
            Buffer.BlockCopy(finalBlock, 0, outputBuffer, outputOffset, finalBlock.Length);

            // Compute the hashes for the final encrypted/decrypted block
            this.sha1.TransformFinalBlock(outputBuffer, 0, outputBuffer.Length);
            this.md5.TransformFinalBlock(outputBuffer, 0, outputBuffer.Length);

            short padding;
            CalculateEncryptedFileSize(this.sourceFileSize, out padding);

            if (padding > 0)
            {
                Array.Resize(ref outputBuffer, outputBuffer.Length + padding);
            }

            this.outputStream.Write(outputBuffer, 0, outputBuffer.Length);

            return outputBuffer.Length;
        }

        private void WriteFirstEncryptedBlock(MemoryStream bufferedOutputStream)
        {
            // Create the encryptor that will be used to transform the data. This will use the key and IV 
            // that were generated when the aes object was created, and will only be used for this stream.
            this.cryptoTransform = this.aes.CreateEncryptor();

            this.WriteEncryptedHeader(bufferedOutputStream);

            this.firstBlockTransformed = true;
        }

        private void ReadFirstEncryptedBlock(byte[] inputBuffer, ref int offset, ref int count)
        {
            // Create a temporary stream for reading the input buffer. This will make it easier to read the
            // input buffer and preserve the read position in the buffer.
            using (MemoryStream inputBufferStream = new MemoryStream(inputBuffer, offset, count))
            {
                byte[] key;
                byte[] iv;
                long originalFileLength;
                short padding;

                this.ReadEncryptedHeader(
                    inputBufferStream,
                    out key,
                    out iv,
                    out originalFileLength,
                    out padding);

                // Create the decrypter used to decrypt the file
                this.cryptoTransform = this.aes.CreateDecryptor(key, iv);

                // Update the count and offset based on the size read for the header since these will
                // be used below to decrypt the buffer.
                count -= (int)inputBufferStream.Position - offset;
                offset = (int)inputBufferStream.Position;
            }

            this.firstBlockTransformed = true;
        }

        private void WriteEncrypted(MemoryStream bufferedOutputStream, byte[] inputBuffer, int offset, int count)
        {
            if (!this.firstBlockTransformed)
            {
                this.WriteFirstEncryptedBlock(bufferedOutputStream);
            }

            // Allocate a buffer with size of the data to be read from the input buffer
            byte[] outputBuffer = new byte[count];

            // Tranform (encrypt) the input data
            this.cryptoTransform.TransformBlock(inputBuffer, offset, count, outputBuffer, 0);

            // Write the transformed data to the output stream
            bufferedOutputStream.Write(outputBuffer, 0, outputBuffer.Length);
        }

        private void ReadEncrypted(MemoryStream bufferedOutputStream, byte[] inputBuffer, int offset, int count)
        {
            if (!this.firstBlockTransformed)
            {
                this.ReadFirstEncryptedBlock(inputBuffer, ref offset, ref count);
            }

            // Allocate a buffer with size of the data to be read from the input buffer
            byte[] outputBuffer = new byte[count];

            // Tranform (encrypt) the input data
            this.cryptoTransform.TransformBlock(inputBuffer, offset, count, outputBuffer, 0);

            // Write the transformed data to the output stream
            bufferedOutputStream.Write(outputBuffer, 0, outputBuffer.Length);
        }

        private void ReadEncryptedHeader(
            MemoryStream inputStream,
            out byte[] key,
            out byte[] iv,
            out long originalFileLength,
            out short padding)
        {
            int encryptedKeyLength = inputStream.ReadInt32();

            // Ensure the key is not longer than the buffer that is supposed to contain it
            Pre.Assert(
                encryptedKeyLength <= DefaultEncryptedKeyStorageSize,
                "encryptedKeyLength <= DefaultEncryptedKeyStorageSize");

            // Read the encrypted key
            byte[] keyEncrypted= new byte[DefaultEncryptedKeyStorageSize];
            inputStream.Read(keyEncrypted, 0, DefaultEncryptedKeyStorageSize);

            // Resize the buffer to be the correct length of the key
            Array.Resize(ref keyEncrypted, encryptedKeyLength);

            // Decrypt the key. 
            key = this.rsa.Decrypt(keyEncrypted, false);

            // Read the initialization vector length
            int ivLength = inputStream.ReadInt32();

            Pre.Assert(
                ivLength == DefaultIVStorageSize,
                "ivLength == DefaultIVStorageSize");

            // Read the initialization vector
            iv = new byte[ivLength];
            inputStream.Read(iv, 0, ivLength);

            // Read the original file size and padding
            originalFileLength = inputStream.ReadInt64();
            padding = inputStream.ReadInt16();
        }

        private void WriteEncryptedHeader(MemoryStream bufferedOutputStream)
        {
            // Create the key formatter that will be used to encrypt the key before it is written to the 
            // output stream.
            RSAPKCS1KeyExchangeFormatter keyFormatter = new RSAPKCS1KeyExchangeFormatter(this.rsa);
            byte[] keyEncrypted = keyFormatter.CreateKeyExchange(this.aes.Key, this.aes.GetType());

            // Ensure that the encrypted key will find into the header
            Pre.Assert(
                keyEncrypted.Length <= DefaultEncryptedKeyStorageSize,
                "keyEncrypted.Length <= DefaultEncryptedKeyStorageSize");

            // Create the full-length buffer where the key will be stored. This is the complete byte array
            // that will be written to to file header. It is declared separately to ensure that the length
            // of the array is correct.
            byte[] keyEncryptedBuffer = new byte[DefaultEncryptedKeyStorageSize];

            // Copy the encrypted key into the header buffer
            Buffer.BlockCopy(keyEncrypted, 0, keyEncryptedBuffer, 0, keyEncrypted.Length);

            // Write the encrypted key length and key into the temporary buffer
            bufferedOutputStream.Write(BitConverter.GetBytes(keyEncrypted.Length), 0, 4);
            bufferedOutputStream.Write(keyEncrypted, 0, keyEncrypted.Length);

            // Ensure that the initialization vector is of the correct length.
            Pre.Assert(
                this.aes.IV.Length == DefaultIVStorageSize,
                "this.aes.IV.Length == DefaultIVStorageSize");

            // Write the initialization vector and length to the temporary buffer
            bufferedOutputStream.Write(BitConverter.GetBytes(this.aes.IV.Length), 0, 4);
            bufferedOutputStream.Write(this.aes.IV, 0, this.aes.IV.Length);

            short padding;
            CalculateEncryptedFileSize(this.sourceFileSize, out padding);

            // Write the original file size and padding
            bufferedOutputStream.Write(BitConverter.GetBytes(this.sourceFileSize), 0, 8);
            bufferedOutputStream.Write(BitConverter.GetBytes(padding), 0, 2);
        }

        public void Dispose()
        {
            this.cryptoTransform?.Dispose();

            this.aes?.Dispose();

            this.rsa?.Dispose();

            this.md5?.Dispose();
            this.sha1?.Dispose();
        }

        /// <summary>
        /// Calculate the length of the encrypted data given the plaintext size. 
        /// </summary>
        /// <param name="unencryptedSize">The size of the plaintext data</param>
        /// <param name="padding"></param>
        /// <returns>The length of the encrypted data</returns>
        public static long CalculateEncryptedFileSize(long unencryptedSize, out short padding)
        {
            // The standard formula for calculating the encrypted size of a file is:
            // EncryptedKeySize + EncryptedKey + IVSize + IV + Plaintext + BlockSize - (Plaintext MOD Blocksize)
            // See: https://stackoverflow.com/questions/7907359/get-the-length-of-a-cryptostream-in-net
            // However, this case we need to include the padding (Plaintext MOD Blocksize) so that we can 
            // determine the unencrypted file's size. Also, we want to include the original file's size as a 
            // check against the result when decrypting. 

            // The encrypted file format is as follows:
            // +-----------------------------------------+   --
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
            // |         padding.Length (2 bytes)        |    |
            // +-----------------------------------------+   --
            // |             encryptedData               |
            // +-----------------------------------------+
            // |                padding                  |
            // +-----------------------------------------+

            const int BlockSizeInBytes = DefaultCspBlockSize / 8;

            // Calculate the header 
            const int HeaderSize =
                sizeof(int) +
                DefaultEncryptedKeyStorageSize +
                sizeof(int) +
                DefaultIVStorageSize +
                sizeof(long) +
                sizeof(short);

            padding = (short)(unencryptedSize % BlockSizeInBytes);

            return HeaderSize + unencryptedSize + BlockSizeInBytes;
        }

        /// <summary>
        /// Calculate the length of the decrypted data given the encrypted size. 
        /// </summary>
        /// <param name="encryptedSize">The size of the plaintext data</param>
        /// <param name="padding"></param>
        /// <returns>The length of the encrypted data</returns>
        public static long CalculateDecryptedFileSize(long encryptedSize, out short padding)
        {
            const int BlockSizeInBytes = DefaultCspBlockSize / 8;

            // Calculate the header 
            const int HeaderSize =
                sizeof(int) +
                DefaultEncryptedKeyStorageSize +
                sizeof(int) +
                DefaultIVStorageSize +
                sizeof(long) +
                sizeof(short);

            padding = (short)(encryptedSize % BlockSizeInBytes);

            return encryptedSize - (HeaderSize + BlockSizeInBytes);
        }
    }
}