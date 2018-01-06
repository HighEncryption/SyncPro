namespace SyncPro.UnitTests
{
    using System.Security;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using Newtonsoft.Json;

    using SyncPro.Adapters;
    using SyncPro.Adapters.BackblazeB2;
    using SyncPro.Utility;

    [TestClass]
    public class JsonSerializationTests
    {
        [TestMethod]
        public void SecureStringToProtectedData()
        {
            AdapterRegistry.RegisterAdapter(
                BackblazeB2Adapter.TargetTypeId,
                typeof(BackblazeB2Adapter),
                typeof(BackblazeB2AdapterConfiguration));

            BackblazeB2AdapterConfiguration config = new BackblazeB2AdapterConfiguration();

            config.AccountId = "1234";
            config.ApplicationKey = new SecureString();
            config.ApplicationKey.AppendChar('a');
            config.ApplicationKey.AppendChar('b');
            config.ApplicationKey.AppendChar('c');
            config.ApplicationKey.AppendChar('1');
            config.ApplicationKey.AppendChar('2');
            config.ApplicationKey.AppendChar('3');

            string s1 = JsonConvert.SerializeObject(config);

            BackblazeB2AdapterConfiguration config2 = 
                JsonConvert.DeserializeObject<BackblazeB2AdapterConfiguration>(s1);

            Assert.IsNotNull(config2);
            Assert.IsNotNull(config2.ApplicationKey);
            Assert.AreEqual(6, config2.ApplicationKey.Length);
            Assert.AreEqual("abc123", config2.ApplicationKey.GetDecrytped());
        }

    }
}