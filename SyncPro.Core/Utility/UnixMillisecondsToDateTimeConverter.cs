namespace SyncPro.Utility
{
    using System;

    using Newtonsoft.Json;

    public class UnixMillisecondsToDateTimeConverter : JsonConverter
    {
        public static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1);

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                serializer.Serialize(writer, null);
                return;
            }

            if (!(value is DateTime))
            {
                throw new NotImplementedException("Only the type DateTime can be converted.");
            }

            DateTime dateTimeValue = (DateTime)value;

            TimeSpan timeSpan = dateTimeValue - UnixEpoch;

            writer.WriteValue(timeSpan.TotalMilliseconds);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader?.Value == null)
            {
                return serializer.Deserialize(reader, null);
            }

            long longValue = Convert.ToInt64(reader.Value);

            return UnixEpoch.AddMilliseconds(longValue);
        }

        public override bool CanConvert(Type objectType)
        {
            throw new NotImplementedException();
        }
    }
}