namespace SyncPro.Runtime
{
    using System;
    using System.IO;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;

    using SyncPro.Adapters;

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

            this.Initialize();
        }

        private void Initialize()
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
            Pre.Assert(count >= EncryptedFileHeader.HeaderSize + 16);

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

                this.outputStream.Write(transformedBuffer, 0, transformedBuffer.Length);

                return transformedBuffer.Length;
            }
        }

        public int TransformFinalBlock(byte[] inputBuffer, int offset, int count)
        {
            byte[] outputBuffer = new byte[0];
            int outputOffset = 0;
            short padding;

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

            if (this.Mode == EncryptionMode.Decrypt)
            {
                CalculateDecryptedFileSize(this.sourceFileSize, out padding);
                count -= padding;
            }

            // Encrypt/decrypt the final block
            byte[] finalBlock = this.cryptoTransform.TransformFinalBlock(inputBuffer, offset, count);

            Array.Resize(ref outputBuffer, outputBuffer.Length + finalBlock.Length);
            Buffer.BlockCopy(finalBlock, 0, outputBuffer, outputOffset, finalBlock.Length);

            // Compute the hashes for the final encrypted/decrypted block
            this.sha1.TransformFinalBlock(outputBuffer, 0, outputBuffer.Length);
            this.md5.TransformFinalBlock(outputBuffer, 0, outputBuffer.Length);

            if (this.Mode == EncryptionMode.Encrypt)
            {
                CalculateEncryptedFileSize(this.sourceFileSize, out padding);

                if (padding > 0)
                {
                    Array.Resize(ref outputBuffer, outputBuffer.Length + padding);
                }
            }

            this.outputStream.Write(outputBuffer, 0, outputBuffer.Length);

            return outputBuffer.Length;
        }

        private void WriteFirstEncryptedBlock(MemoryStream bufferedOutputStream)
        {
            // Create the encryptor that will be used to transform the data. This will use the key and IV 
            // that were generated when the aes object was created, and will only be used for this stream.
            this.cryptoTransform = this.aes.CreateEncryptor();

            RSAPKCS1KeyExchangeFormatter keyFormatter = new RSAPKCS1KeyExchangeFormatter(this.rsa);
            byte[] keyEncrypted = keyFormatter.CreateKeyExchange(this.aes.Key, this.aes.GetType());

            EncryptedFileHeader header = new EncryptedFileHeader
            {
                EncryptedKey = keyEncrypted,
                IV = this.aes.IV,
                CertificateThumbprint = AdapterBase.HexToBytes(this.encryptionCertificate.Thumbprint)
            };

            short padding;

            if (this.Mode == EncryptionMode.Encrypt)
            {
                header.OriginalFileLength = this.sourceFileSize;
                header.EncryptedFileLength = CalculateEncryptedFileSize(this.sourceFileSize, out padding);
                header.PaddingLength = padding;
            }
            else
            {
                header.EncryptedFileLength = this.sourceFileSize;
                header.OriginalFileLength = CalculateDecryptedFileSize(this.sourceFileSize, out padding);
                header.PaddingLength = padding;
            }

            header.WriteToStream(bufferedOutputStream);

            this.firstBlockTransformed = true;
        }

        private void ReadFirstEncryptedBlock(byte[] inputBuffer, ref int offset, ref int count)
        {
            // Create a temporary stream for reading the input buffer. This will make it easier to read the
            // input buffer and preserve the read position in the buffer.
            using (MemoryStream inputBufferStream = new MemoryStream(inputBuffer, offset, count))
            {
                EncryptedFileHeader header = EncryptedFileHeader.ReadFromStream(inputBufferStream);

                // Decrypt the key. 
                byte[] key = this.rsa.Decrypt(header.EncryptedKey, false);

                // Create the decrypter used to decrypt the file
                this.cryptoTransform = this.aes.CreateDecryptor(key, header.IV);

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
            int transformCount = this.cryptoTransform.TransformBlock(inputBuffer, offset, count, outputBuffer, 0);

            // Write the transformed data to the output stream
            bufferedOutputStream.Write(outputBuffer, 0, transformCount);
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
            //
            const int BlockSizeInBytes = DefaultCspBlockSize / 8;

            padding = (short)(unencryptedSize % BlockSizeInBytes);

            return EncryptedFileHeader.HeaderSize + unencryptedSize + BlockSizeInBytes;
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

            // The encrypted data always has a length according to the following formula:
            //   encryptedSize = plainTextSize + blockSize - (plainText % blocksize)
            padding = (short)((encryptedSize - EncryptedFileHeader.HeaderSize) % BlockSizeInBytes);

            return encryptedSize - (EncryptedFileHeader.HeaderSize + BlockSizeInBytes);
        }
    }
}