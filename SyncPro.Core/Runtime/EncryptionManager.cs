namespace SyncPro.Runtime
{
    using System;
    using System.IO;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;

    using SyncPro.Adapters;
    using SyncPro.Configuration;

    /// <summary>
    /// This class manages the encryption/decryption of files along with the calculating of hashes
    /// before/after encryption.
    /// </summary>
    /// <remarks>
    /// The EncryptionManager class performs either encryption or decryption of files (known as
    /// transforms) using the secrets contained in the X509Certificate2 provided. Each file that
    /// is encrypted will have its own encryptor (key and IV) generated, which will be stored
    /// in the transformed content. The encryptor is itself encrypted with the X509Certificate2
    /// prior to being stored. This allows strong protection of each file, while allowing a single
    /// certificate to be able to decrypted multiple files.
    /// During the encryption/decryption process, the SHA1 and MD5 hashes of the content are
    /// calculated as the content is transformed. Data is tranformed in blocks (as opposed to the
    /// entire content at once), which allows for hashes to be calculated for both the pre-transform
    /// and post-transform content, while eliminating the need to read the content multiple times.
    /// An <see cref="EncryptionManager"/> instance SHOULD be created for each file so that a
    /// unique encrypted or created. Without this, files will have reduced security.
    /// </remarks>
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

        // An encrypted file will contain a 1k header plus one 16-byte block
        private const int MinimumEncryptedFileSize = 1040;

        // The certificate that contains the secrets using for encryption.
        private readonly X509Certificate2 encryptionCertificate;

        private readonly long sourceFileSize;

        // Hash algorithm managed resources. These will be used to hash the data 
        // as is passes through the EncryptionManager.
        private readonly SHA1Cng sha1;
        private readonly MD5Cng md5;

        // The stream where transformed content will be written
        private readonly Stream outputStream;

        // Inidicates whether the first block of data has been transformed yet
        private bool firstBlockTransformed;

        // CryptoServiceProvider object for performing encryption/decryption/hashing
        private RSACryptoServiceProvider rsa;
        private AesCryptoServiceProvider aes;
        private ICryptoTransform cryptoTransform;

        public EncryptionMode Mode { get; }

        public byte[] Sha1Hash => this.sha1.Hash;

        public byte[] Md5Hash => this.md5.Hash;

        /// <summary>
        /// Create a new instance of the <see cref="EncryptionManager"/> class
        /// </summary>
        /// <param name="encryptionCertificate">
        /// The certificate that contains the secrets used for encryption. Note that
        /// the private key must be present in this certificate in order for decryption
        /// to be performed.
        /// </param>
        /// <param name="mode">The mode (encryption/decryption) of the encryption manager</param>
        /// <param name="outputStream">The stream where the transformed content will be written</param>
        /// <param name="sourceFileSize"></param>
        public EncryptionManager(
            X509Certificate2 encryptionCertificate, 
            EncryptionMode mode,
            Stream outputStream,
            long sourceFileSize)
        {
            Pre.ThrowIfArgumentNull(encryptionCertificate, nameof(encryptionCertificate));
            Pre.ThrowIfArgumentNull(outputStream, nameof(outputStream));

            Pre.ThrowIfTrue(mode == EncryptionMode.None, "Encryption mode cannot be None");

            this.encryptionCertificate = encryptionCertificate;
            this.Mode = mode;
            this.sourceFileSize = sourceFileSize;
            this.outputStream = outputStream;

            this.sha1 = new SHA1Cng();
            this.md5 = new MD5Cng();

            // Any valid encrypted file will have a minimum size (to include the header and minimal
            // encrypted content). Ensure that the source file is at least this size.
            if (mode == EncryptionMode.Decrypt)
            {
                Pre.Assert(sourceFileSize >= MinimumEncryptedFileSize, "sourceFileSize >= minimumEncryptedFileSize");
            }

            this.Initialize();
        }

        /// <summary>
        /// Initialize the <see cref="EncryptionManager"/>. This will initialize the CryptoServiceProvider
        /// used to perform encryption, as well as to verify that the certificate is suitable for the
        /// desired mode.
        /// </summary>
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

        /// <summary>
        /// Transform a block by encrypting/decrypting the file.
        /// </summary>
        /// <param name="buffer">The data to transform</param>
        /// <param name="offset">The offset within the buffer to begin reading</param>
        /// <param name="count">The number of bytes from the buffer to read</param>
        /// <returns>The number of bytes transformed</returns>
        public int TransformBlock(byte[] buffer, int offset, int count)
        {
            Pre.Assert(count >= EncryptedFileHeader.HeaderSize + 16);

            // Create a new memory stream that will hold the transformed content, as well as allow
            // for hashes to be calculated as the data is streamed.
            using (MemoryStream bufferedOutputStream = new MemoryStream())
            {
                // Call the appropriate method to encrypt/decrypt the data
                if (this.Mode == EncryptionMode.Encrypt)
                {
                    this.WriteEncrypted(bufferedOutputStream, buffer, offset, count);
                }
                else
                {
                    this.ReadEncrypted(bufferedOutputStream, buffer, offset, count);
                }

                // Read the tranformed data as a byte array for each hash calculation.
                byte[] transformedBuffer = bufferedOutputStream.ToArray();

                // Compute the ongoing hash value
                this.sha1.TransformBlock(transformedBuffer, 0, transformedBuffer.Length, null, 0);
                this.md5.TransformBlock(transformedBuffer, 0, transformedBuffer.Length, null, 0);

                // Write the transformed data to the output stream.
                this.outputStream.Write(transformedBuffer, 0, transformedBuffer.Length);

                return transformedBuffer.Length;
            }
        }

        /// <summary>
        /// Transform the final block of data.
        /// </summary>
        /// <param name="inputBuffer">The data to transform</param>
        /// <param name="offset">The offset within the input buffer to start reading</param>
        /// <param name="count">The number of bytes to read from the input buffer</param>
        /// <returns>The number of bytes transformed</returns>
        public int TransformFinalBlock(byte[] inputBuffer, int offset, int count)
        {
            byte[] outputBuffer = new byte[0];
            int outputOffset = 0;
            short padding;

            // If no data has been transformed yet (meaning that the data to be transformed was 
            // small enough that TransformBlock was not called), then we need to perform steps
            // that are performed there for handling the initial block of data.
            if (!this.firstBlockTransformed)
            {
                if (this.Mode == EncryptionMode.Encrypt)
                {
                    // For encrypting, we need to write the encrypted file header
                    using (MemoryStream bufferedOutputStream = new MemoryStream())
                    {
                        this.WriteFirstEncryptedBlock(bufferedOutputStream);
                        outputBuffer = bufferedOutputStream.ToArray();
                        outputOffset = outputBuffer.Length;
                    }
                }
                else
                {
                    // For decrypting, we need to read the encrypted file header
                    this.ReadFirstEncryptedBlock(inputBuffer, ref offset, ref count);
                }
            }

            if (this.Mode == EncryptionMode.Decrypt)
            {
                CalculateDecryptedFileSize(this.sourceFileSize, out padding);
                count -= padding;
            }

            // Encrypt/decrypt the final block. Note that unlike TransformBlock, the final block of
            // transformed data is returned.
            byte[] finalBlock = this.cryptoTransform.TransformFinalBlock(inputBuffer, offset, count);

            // Resize the output buffer to be long enough to fit the final transformed block and
            // copy the final transformed block onto the end of the output buffer.
            Array.Resize(ref outputBuffer, outputBuffer.Length + finalBlock.Length);
            Buffer.BlockCopy(finalBlock, 0, outputBuffer, outputOffset, finalBlock.Length);

            // Compute the hashes for the final encrypted/decrypted block
            this.sha1.TransformFinalBlock(outputBuffer, 0, outputBuffer.Length);
            this.md5.TransformFinalBlock(outputBuffer, 0, outputBuffer.Length);

            // If we are encrypting data, the file needs to have a specific increment in size, which
            // may require padding at the end of the file.
            if (this.Mode == EncryptionMode.Encrypt)
            {
                CalculateEncryptedFileSize(this.sourceFileSize, out padding);

                if (padding > 0)
                {
                    Array.Resize(ref outputBuffer, outputBuffer.Length + padding);
                }
            }

            // Write the final buffer of data to the output stream
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
            Pre.Assert(encryptedSize >= MinimumEncryptedFileSize, "encryptedSize >= minimumEncryptedFileSize");

            const int BlockSizeInBytes = DefaultCspBlockSize / 8;

            // The encrypted data always has a length according to the following formula:
            //   encryptedSize = plainTextSize + blockSize - (plainText % blocksize)
            padding = (short)((encryptedSize - EncryptedFileHeader.HeaderSize) % BlockSizeInBytes);

            Pre.Assert(padding >= 0, "padding >= 0");

            return encryptedSize - (EncryptedFileHeader.HeaderSize + BlockSizeInBytes);
        }
    }
}