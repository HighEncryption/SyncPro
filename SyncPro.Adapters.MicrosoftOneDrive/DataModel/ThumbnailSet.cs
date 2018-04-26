namespace SyncPro.Adapters.MicrosoftOneDrive.DataModel
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [JsonConverter(typeof(ThumbnailSetConverter))]
    public class ThumbnailSet
    {
        public string Id { get; set; }

        public Dictionary<string,ThumbnailInfo> Thumbnails { get; set; }
    }

    public class ThumbnailSetConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);

            JToken tok = jObject.GetValue("value");

            ThumbnailSet set = new ThumbnailSet
            {
                Thumbnails = new Dictionary<string, ThumbnailInfo>()
            };

            foreach (JToken jToken in tok.Children().Children())
            {
                if (jToken is JProperty jProp)
                {
                    if (jProp.Name == "id")
                    {
                        set.Id = jProp.Value.Value<string>();
                        continue;
                    }

                    set.Thumbnails.Add(
                        jProp.Name,
                        jProp.Value.ToObject<ThumbnailInfo>());
                }
            }

            return set;
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(ThumbnailSet);
        }
    }
}