namespace SyncPro.UnitTests
{
    using System;
    using System.IO;
    using System.Security.Cryptography.X509Certificates;

    using JsonLog;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SyncPro.Runtime;

    [TestClass]
    public class EncryptionTests
    {
        public TestContext TestContext { get; set; }

        private static X509Certificate2 ReadTestCert()
        {
            string certificateFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "SyncProEncTest.pfx");

            if (!File.Exists(certificateFilePath))
            {
                throw new Exception("The certificate file was not found at the path " + certificateFilePath);
            }

            return new X509Certificate2(certificateFilePath);
        }

        public void TestCleanup()
        {
            Logger.Info("Test completed");
        }

        [TestMethod]
        public void SyncLocalFilesWithEncryption()
        {
            var wrapper = TestWrapperFactory
                .CreateLocalToLocal(this.TestContext)
                .SaveRelationship()
                .CreateSimpleSourceStructure();

            wrapper.Relationship.EncryptionIsEnabled = true;
            wrapper.Relationship.EncryptionCertificateThumbprint = "420fe8033179cfb0ef21862d24bf6a1ec7df6c6d";

            wrapper.CreateSyncRun()
                .RunToCompletion()
                .VerifySyncSuccess()
                .VerifyResultContainsAllFiles()
                .VerifyDatabaseHashes();
        }

        [TestMethod]
        public void EncryptAndDecryptFile1Byte()
        {
            byte[] inputBuffer = { 0xab };
            MemoryStream outputStream = null;
            EncryptionManager encryptionManager = null;
            byte[] encryptedData;
            byte[] decryptedData;
            try
            {
                outputStream = new MemoryStream();

                encryptionManager = new EncryptionManager(
                    ReadTestCert(), 
                    EncryptionMode.Encrypt,
                    outputStream,
                    inputBuffer.Length);

                int written = encryptionManager.TransformFinalBlock(inputBuffer, 0, inputBuffer.Length);

                encryptedData = outputStream.ToArray();

                Assert.AreEqual(encryptedData.Length, written);
            }
            finally
            {
                encryptionManager?.Dispose();
                outputStream?.Dispose();
            }

            try
            {
                outputStream = new MemoryStream();

                encryptionManager = new EncryptionManager(
                    ReadTestCert(),
                    EncryptionMode.Decrypt,
                    outputStream,
                    encryptedData.Length);

                int written = encryptionManager.TransformFinalBlock(encryptedData, 0, encryptedData.Length);

                decryptedData = outputStream.ToArray();

                Assert.AreEqual(decryptedData.Length, written);
            }
            finally
            {
                encryptionManager?.Dispose();
                outputStream?.Dispose();
            }

            Assert.AreEqual(1, decryptedData.Length);
            Assert.AreEqual(0xab, decryptedData[0]);
        }

        [TestMethod]
        public void EncryptAndDecryptFile16Bytes()
        {
            byte[] inputBuffer = CreateInputBuffer(16);

            MemoryStream outputStream = null;
            EncryptionManager encryptionManager = null;
            byte[] encryptedData;
            byte[] decryptedData;
            try
            {
                outputStream = new MemoryStream();

                encryptionManager = new EncryptionManager(
                    ReadTestCert(), 
                    EncryptionMode.Encrypt,
                    outputStream,
                    inputBuffer.Length);

                int written = encryptionManager.TransformFinalBlock(inputBuffer, 0, inputBuffer.Length);

                encryptedData = outputStream.ToArray();

                Assert.AreEqual(encryptedData.Length, written);
            }
            finally
            {
                encryptionManager?.Dispose();
                outputStream?.Dispose();
            }

            try
            {
                outputStream = new MemoryStream();

                encryptionManager = new EncryptionManager(
                    ReadTestCert(),
                    EncryptionMode.Decrypt,
                    outputStream,
                    encryptedData.Length);

                int written = encryptionManager.TransformFinalBlock(encryptedData, 0, encryptedData.Length);

                decryptedData = outputStream.ToArray();

                Assert.AreEqual(decryptedData.Length, written);
            }
            finally
            {
                encryptionManager?.Dispose();
                outputStream?.Dispose();
            }

            Assert.AreEqual(16, decryptedData.Length);
            for (int i = 0; i < 16; i++)
            {
                Assert.AreEqual((byte)i, decryptedData[i]);
            }
        }

        [TestMethod]
        public void EncryptAndDecryptFile10KBytes()
        {
            byte[] inputBuffer = CreateInputBuffer(10 * 1024); // 10k

            short padding;
            long expectedEncryptedSize =
                EncryptionManager.CalculateEncryptedFileSize(inputBuffer.Length, out padding);

            MemoryStream inputStream = null;
            MemoryStream outputStream = null;

            EncryptionManager encryptionManager = null;
            byte[] encryptedData;
            byte[] decryptedData;
            try
            {
                inputStream = new MemoryStream(inputBuffer);
                outputStream = new MemoryStream();

                encryptionManager = new EncryptionManager(
                    ReadTestCert(), 
                    EncryptionMode.Encrypt,
                    outputStream,
                    inputBuffer.Length);

                int written = 0;
                int blockSize = 2048;
                byte[] buf = new byte[blockSize];

                while (true)
                {
                    int read = inputStream.Read(buf, 0, blockSize);
                    if (read == blockSize)
                    {
                        written += encryptionManager.TransformBlock(buf, 0, read);
                    }
                    else
                    {
                        // Read the end of the input stream
                        written += encryptionManager.TransformFinalBlock(buf, 0, read);
                        break;
                    }
                }

                encryptedData = outputStream.ToArray();

                Assert.AreEqual(expectedEncryptedSize, encryptedData.Length);
                Assert.AreEqual(encryptedData.Length, written);
            }
            finally
            {
                encryptionManager?.Dispose();
                outputStream?.Dispose();
                inputStream?.Dispose();
            }

            long expectedDecryptedSize = 
                EncryptionManager.CalculateDecryptedFileSize(expectedEncryptedSize, out padding);

            Assert.AreEqual(expectedDecryptedSize, inputBuffer.Length);

            try
            {
                inputStream = new MemoryStream(encryptedData);
                outputStream = new MemoryStream();

                encryptionManager = new EncryptionManager(
                    ReadTestCert(),
                    EncryptionMode.Decrypt,
                    outputStream,
                    encryptedData.Length);

                int written = 0;
                int blockSize = 2048;
                byte[] buf = new byte[blockSize];

                while (true)
                {
                    int read = inputStream.Read(buf, 0, blockSize);
                    if (read == blockSize)
                    {   
                        written += encryptionManager.TransformBlock(buf, 0, read);
                    }
                    else
                    {
                        // Read the end of the input stream
                        written += encryptionManager.TransformFinalBlock(buf, 0, read);
                        break;
                    }
                }

                decryptedData = outputStream.ToArray();

                Assert.AreEqual(decryptedData.Length, written);
                Assert.AreEqual(inputBuffer.Length, decryptedData.Length);
            }
            finally
            {
                encryptionManager?.Dispose();
                outputStream?.Dispose();
                inputStream?.Dispose();
            }

            for (int i = 0; i < decryptedData.Length; i++)
            {
                if (decryptedData[i] != i % 256)
                {
                    Assert.Fail("Invalid data at byte " + i);
                }
            }
        }

        [TestMethod]
        public void EncryptAndDecryptFilePartialBlock()
        {
            byte[] inputBuffer = CreateInputBuffer(2048 + 8);

            short padding;
            long expectedEncryptedSize =
                EncryptionManager.CalculateEncryptedFileSize(inputBuffer.Length, out padding);

            MemoryStream inputStream = null;
            MemoryStream outputStream = null;

            EncryptionManager encryptionManager = null;
            byte[] encryptedData;
            byte[] decryptedData;
            try
            {
                inputStream = new MemoryStream(inputBuffer);
                outputStream = new MemoryStream();

                encryptionManager = new EncryptionManager(
                    ReadTestCert(), 
                    EncryptionMode.Encrypt,
                    outputStream,
                    inputBuffer.Length);

                int written = 0;
                int blockSize = 2048;
                byte[] buf = new byte[blockSize];

                while (true)
                {
                    int read = inputStream.Read(buf, 0, blockSize);
                    if (read == blockSize)
                    {
                        written += encryptionManager.TransformBlock(buf, 0, read);
                    }
                    else
                    {
                        // Read the end of the input stream
                        written += encryptionManager.TransformFinalBlock(buf, 0, read);
                        break;
                    }
                }

                encryptedData = outputStream.ToArray();

                Assert.AreEqual(expectedEncryptedSize, encryptedData.Length);
                Assert.AreEqual(encryptedData.Length, written);
            }
            finally
            {
                encryptionManager?.Dispose();
                outputStream?.Dispose();
                inputStream?.Dispose();
            }

            long expectedDecryptedSize = 
                EncryptionManager.CalculateDecryptedFileSize(expectedEncryptedSize, out padding);

            Assert.AreEqual(expectedDecryptedSize, inputBuffer.Length);

            try
            {
                inputStream = new MemoryStream(encryptedData);
                outputStream = new MemoryStream();

                encryptionManager = new EncryptionManager(
                    ReadTestCert(),
                    EncryptionMode.Decrypt,
                    outputStream,
                    encryptedData.Length);

                int written = 0;
                int blockSize = 2048;
                byte[] buf = new byte[blockSize];

                while (true)
                {
                    int read = inputStream.Read(buf, 0, blockSize);
                    if (read == blockSize)
                    {   
                        written += encryptionManager.TransformBlock(buf, 0, read);
                    }
                    else
                    {
                        // Read the end of the input stream
                        written += encryptionManager.TransformFinalBlock(buf, 0, read);
                        break;
                    }
                }

                decryptedData = outputStream.ToArray();

                Assert.AreEqual(decryptedData.Length, written);
                Assert.AreEqual(inputBuffer.Length, decryptedData.Length);
            }
            finally
            {
                encryptionManager?.Dispose();
                outputStream?.Dispose();
                inputStream?.Dispose();
            }

            for (int i = 0; i < decryptedData.Length; i++)
            {
                if (decryptedData[i] != i % 256)
                {
                    Assert.Fail("Invalid data at byte " + i);
                }
            }
        }

        [TestMethod]
        public void EncryptAndDecryptFileExactBlockBoundary()
        {
            byte[] inputBuffer = CreateInputBuffer(EncryptedFileHeader.HeaderSize);

            short padding;
            long expectedEncryptedSize =
                EncryptionManager.CalculateEncryptedFileSize(inputBuffer.Length, out padding);

            MemoryStream inputStream = null;
            MemoryStream outputStream = null;

            EncryptionManager encryptionManager = null;
            byte[] encryptedData;
            byte[] decryptedData;
            try
            {
                inputStream = new MemoryStream(inputBuffer);
                outputStream = new MemoryStream();

                encryptionManager = new EncryptionManager(
                    ReadTestCert(), 
                    EncryptionMode.Encrypt,
                    outputStream,
                    inputBuffer.Length);

                int written = 0;
                int blockSize = 2048;
                byte[] buf = new byte[blockSize];

                while (true)
                {
                    int read = inputStream.Read(buf, 0, blockSize);
                    if (read == blockSize)
                    {
                        written += encryptionManager.TransformBlock(buf, 0, read);
                    }
                    else
                    {
                        // Read the end of the input stream
                        written += encryptionManager.TransformFinalBlock(buf, 0, read);
                        break;
                    }
                }

                encryptedData = outputStream.ToArray();

                Assert.AreEqual(expectedEncryptedSize, encryptedData.Length);
                Assert.AreEqual(encryptedData.Length, written);
            }
            finally
            {
                encryptionManager?.Dispose();
                outputStream?.Dispose();
                inputStream?.Dispose();
            }

            long expectedDecryptedSize = 
                EncryptionManager.CalculateDecryptedFileSize(expectedEncryptedSize, out padding);

            Assert.AreEqual(expectedDecryptedSize, inputBuffer.Length);

            try
            {
                inputStream = new MemoryStream(encryptedData);
                outputStream = new MemoryStream();

                encryptionManager = new EncryptionManager(
                    ReadTestCert(),
                    EncryptionMode.Decrypt,
                    outputStream,
                    encryptedData.Length);

                int written = 0;
                int blockSize = 2048;
                byte[] buf = new byte[blockSize];

                while (true)
                {
                    int read = inputStream.Read(buf, 0, blockSize);
                    if (read == blockSize)
                    {   
                        written += encryptionManager.TransformBlock(buf, 0, read);
                    }
                    else
                    {
                        // Read the end of the input stream
                        written += encryptionManager.TransformFinalBlock(buf, 0, read);
                        break;
                    }
                }

                decryptedData = outputStream.ToArray();

                Assert.AreEqual(decryptedData.Length, written);
                Assert.AreEqual(inputBuffer.Length, decryptedData.Length);
            }
            finally
            {
                encryptionManager?.Dispose();
                outputStream?.Dispose();
                inputStream?.Dispose();
            }

            for (int i = 0; i < decryptedData.Length; i++)
            {
                if (decryptedData[i] != i % 256)
                {
                    Assert.Fail("Invalid data at byte " + i);
                }
            }
        }

        /// <summary>
        /// Test file encryption of a single byte
        /// </summary>
        [TestMethod]
        public void VerifyEncryptedSizeCalculations1Byte()
        {
            /*
             *  Test with 1 byte input
             */
            short padding;
            var encryptedSize = EncryptionManager.CalculateEncryptedFileSize(1, out padding);

            // Header size is 1k bytes. Expect to see header + blocksize + padding
            int expectedEncryptedSize = EncryptedFileHeader.HeaderSize + 16 + padding;
            Assert.AreEqual(expectedEncryptedSize, encryptedSize);
            Assert.AreEqual(1, padding);
        }

        [TestMethod]
        public void VerifyDecryptedSizeCalculations()
        {
            /*
             *  Test with 1 byte input
             */
            int encryptedFileSize = EncryptedFileHeader.HeaderSize + 16 + 1;
            short padding;
            var decryptedSize = EncryptionManager.CalculateDecryptedFileSize(encryptedFileSize, out padding);

            Assert.AreEqual(1, decryptedSize);
            Assert.AreEqual(1, padding);
        }

        [TestMethod]
        public void VerifyEncryptedSizeCalculationsModBlockSize()
        {
            /*
             *  Test with size that is (MOD BlockSize == 0)
             */
            short padding;
            var encryptedSize = EncryptionManager.CalculateEncryptedFileSize(1024, out padding);

            // Header size is 1k bytes. Expect to see header + plaintext
            Assert.AreEqual(EncryptedFileHeader.HeaderSize + 1024 + 16, encryptedSize);
            Assert.AreEqual(0, padding);
        }

        private static byte[] CreateInputBuffer(int size)
        {
            byte[] buffer = new byte[size];

            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = (byte)(i % 256);
            }

            return buffer;
        }
    }
}
