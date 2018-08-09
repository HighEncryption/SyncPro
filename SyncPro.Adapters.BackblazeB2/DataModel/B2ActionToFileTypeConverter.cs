namespace SyncPro.Adapters.BackblazeB2.DataModel
{
    using System;

    using Newtonsoft.Json;

    public class B2ActionToFileTypeConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                serializer.Serialize(writer, null);
                return;
            }

            if (!(value is bool))
            {
                throw new NotImplementedException("Only the type bool can be converted.");
            }

            writer.WriteValue((bool) value ? "upload" : "folder");
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader?.Value == null)
            {
                return serializer.Deserialize(reader, null);
            }

            string action = reader.Value as string;

            if (action == "upload")
            {
                return true;
            }

            if (action == "folder")
            {
                return false;
            }

            throw new FormatException(
                string.Format("The action '{0}' is not a valid action type", action));
        }

        public override bool CanConvert(Type objectType)
        {
            throw new NotImplementedException();
        }
    }
}