namespace SyncPro.Utility
{
    using System;
    using System.Runtime.InteropServices;
    using System.Security;

    using Newtonsoft.Json;

    public class SecureStringConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                serializer.Serialize(writer, null);
                return;
            }

            SecureString secureString = value as SecureString;
            if (secureString == null)
            {
                throw new NotImplementedException("Only the type SecureString can be converted.");
            }

            IntPtr valuePtr = IntPtr.Zero;

            try
            {
                valuePtr = Marshal.SecureStringToGlobalAllocUnicode(secureString);
                string decryptedString = Marshal.PtrToStringUni(valuePtr);

                writer.WriteValue(decryptedString);
            }
            finally
            {
                Marshal.ZeroFreeGlobalAllocUnicode(valuePtr);
            }
        }

        public override object ReadJson(
            JsonReader reader,
            Type objectType,
            object existingValue,
            JsonSerializer serializer)
        {
            if (reader == null || reader.Value == null)
            {
                return serializer.Deserialize(reader, null);
            }

            string source = reader.Value as string;

            if (source == null)
            {
                return serializer.Deserialize(reader, null);
            }

            SecureString secureString = new SecureString();
            foreach (char c in source)
            {
                secureString.AppendChar(c);
            }

            secureString.MakeReadOnly();
            return secureString;
        }

        public override bool CanConvert(Type objectType)
        {
            throw new NotImplementedException();
        }
    }
}