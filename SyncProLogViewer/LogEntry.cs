namespace SyncProLogViewer
{
    using System;
    using System.IO;
    using System.Text;

    using Newtonsoft.Json;

    public class LogEntry
    {
        [JsonProperty("ts")]
        public DateTime Timestamp { get; set; }

        [JsonProperty("l")]
        public string Level { get; set; }

        [JsonProperty("tid")]
        public int ThreadId { get; set; }

        [JsonProperty("m")]
        public string Message { get; set; }

        [JsonProperty("ap")]
        public string ActivityPath { get; set; }

        public static string Serialize(LogEntry entry)
        {
            StringBuilder sb = new StringBuilder();
            using (StringWriter stringWriter = new StringWriter(sb))
            {
                JsonTextWriter writer = new JsonTextWriter(stringWriter);

                writer.WriteStartObject();

                writer.WritePropertyName("ts");
                writer.WriteValue(entry.Timestamp);

                writer.WritePropertyName("l");
                writer.WriteValue(entry.Level);

                writer.WritePropertyName("tid");
                writer.WriteValue(entry.ThreadId);

                writer.WritePropertyName("m");
                writer.WriteValue(entry.Message);

                writer.WritePropertyName("ap");
                writer.WriteValue(entry.ActivityPath);

                writer.WriteEndObject();
            }

            return sb.ToString();
        }

        public static LogEntry Parse(StringBuilder sb)
        {
            return Parse(sb.ToString());
        }

        public static LogEntry Parse(string strEntry)
        {
            var reader = new JsonTextReader(new StringReader(strEntry));

            LogEntry entry = new LogEntry();
            var currentProperty = string.Empty;

            while (reader.Read())
            {
                if (reader.Value == null)
                {
                    continue;
                }

                if (reader.TokenType == JsonToken.PropertyName)
                {
                    currentProperty = reader.Value.ToString();
                }

                if (reader.TokenType == JsonToken.Date && currentProperty == "ts")
                {
                    entry.Timestamp = (DateTime)reader.Value;
                }

                if (reader.TokenType == JsonToken.String && currentProperty == "l")
                {
                    entry.Level = reader.Value.ToString();
                }

                if (reader.TokenType == JsonToken.Integer && currentProperty == "tid")
                {
                    entry.ThreadId = Int32.Parse(reader.Value.ToString());
                }

                if (reader.TokenType == JsonToken.String && currentProperty == "m")
                {
                    entry.Message = reader.Value.ToString();
                }

                if (reader.TokenType == JsonToken.String && currentProperty == "ap")
                {
                    entry.ActivityPath = reader.Value.ToString();
                }
            }

            return entry;
        }
    }
}
