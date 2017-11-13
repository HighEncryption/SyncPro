namespace SyncPro.Configuration
{
    using System;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Newtonsoft.Json.Serialization;

    using SyncPro.Adapters.MicrosoftOneDrive;
    using SyncPro.Adapters.WindowsFileSystem;

    public class ConcreteClassConverter : DefaultContractResolver
    {
        protected override JsonConverter ResolveContractConverter(Type objectType)
        {
            if (typeof(AdapterConfiguration).IsAssignableFrom(objectType) && !objectType.IsAbstract)
            {
                return null;
            }

            return base.ResolveContractConverter(objectType);
        }
    }

    public class AdapterConfigurationConverter : JsonConverter
    {
        private static readonly JsonSerializerSettings SerializerSettings =
            new JsonSerializerSettings()
            {
                ContractResolver = new ConcreteClassConverter()
            };

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(AdapterConfiguration);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);
            Guid typeId = Guid.Parse(jObject["AdapterTypeId"].Value<string>());

            if (typeId == OneDriveAdapter.TargetTypeId)
            {
                return JsonConvert.DeserializeObject<OneDriveAdapterConfiguration>(
                    jObject.ToString(), 
                    SerializerSettings);
            }
            if (typeId == WindowsFileSystemAdapter.TargetTypeId)
            {
                return JsonConvert.DeserializeObject<WindowsFileSystemAdapterConfiguration>(
                    jObject.ToString(), 
                    SerializerSettings);
            }

            throw new NotImplementedException("Cannot read adapter configuration for type ID " + typeId);
        }

        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}