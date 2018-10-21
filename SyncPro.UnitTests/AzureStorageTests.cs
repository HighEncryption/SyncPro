namespace SyncPro.UnitTests
{
    using System;
    using System.ComponentModel;
    using System.IO;
    using System.Security;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using Newtonsoft.Json;

    using SyncPro.Adapters;
    using SyncPro.Adapters.MicrosoftAzureStorage;
    using SyncPro.Runtime;
    using SyncPro.Utility;

    //public class AzureStorageAccountInfo
    //{
    //    public string AccountName { get; set; }

    //    [JsonConverter(typeof(SecureStringToProtectedDataConverter))]
    //    public SecureString AccessKey { get; set; }

    //    public string ContainerName { get; set; }

    //}

    [TestClass]
    public class AzureStorageTests : AdapterTestsBase<AzureStorageAdapter>
    {
        public const string DefaultContainerName = "syncpro-unit-tests";
        public const string TestingContainerName = "syncpro-temp-tests";

        private static AzureStorageAdapterConfiguration accountInfo;

       
        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            if (!GlobalTestSettings.RunNetworkTests)
            {
                return;
            }

            AdapterRegistry.RegisterAdapter(
                AzureStorageAdapter.TargetTypeId,
                typeof(AzureStorageAdapter),
                typeof(AzureStorageAdapterConfiguration));


            string accountInfoFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "SyncProTesting",
                "AzureStorageAccountInfo.json");

            if (File.Exists(accountInfoFilePath))
            {
                string fileContent = File.ReadAllText(accountInfoFilePath);
                accountInfo = JsonConvert.DeserializeObject<AzureStorageAdapterConfiguration>(fileContent);

                return;
            }

            CredentialResult credentials;
            try
            {
                credentials = CredentialHelper.PromptForCredentials(
                    "Enter your storage account name (username) and account key (password)");
            }
            catch (Win32Exception win32Exception)
                when (win32Exception.NativeErrorCode ==
                      (int) NativeMethods.CredUI.CredUIReturnCodes.ERROR_CANCELLED)
            {
                Assert.Inconclusive("Azure storage credentials are required to run tests");

                // ReSharper disable once HeuristicUnreachableCode
                return;
            }

            accountInfo = new AzureStorageAdapterConfiguration
            {
                AccountName = credentials.Username,
                AccountKey = credentials.Password,
                ContainerName = DefaultContainerName
            };

            string serializedInfo = JsonConvert.SerializeObject(accountInfo, Formatting.Indented);
            File.WriteAllText(accountInfoFilePath, serializedInfo);
        }

        protected override AzureStorageAdapter CreateSourceAdapter(
            SyncRelationship newRelationship,
            string testMethodName)
        {
            AzureStorageAdapter sourceAdapter = new AzureStorageAdapter(newRelationship, accountInfo);

            sourceAdapter.TypedConfiguration.IsOriginator = true;

            sourceAdapter.InitializeClient();

            return sourceAdapter;
        }

        protected override AzureStorageAdapter CreateDestinationAdapter(
            SyncRelationship newRelationship, 
            string testMethodName)
        {
            var testAccountInfo = new AzureStorageAdapterConfiguration
            {
                AccountName = accountInfo.AccountName,
                AccountKey = accountInfo.AccountKey,
                ContainerName = TestingContainerName
            };

            AzureStorageAdapter destAdapter = new AzureStorageAdapter(newRelationship, testAccountInfo);

            destAdapter.TypedConfiguration.IsOriginator = false;

            destAdapter.InitializeClient();

            return destAdapter;
        }
    }
}