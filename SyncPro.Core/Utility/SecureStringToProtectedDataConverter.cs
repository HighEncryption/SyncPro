namespace SyncPro.Utility
{
    using System;
    using System.Runtime.InteropServices;
    using System.Security;
    using System.Security.Cryptography;

    using Newtonsoft.Json;

    public class SecureStringToProtectedDataConverter : JsonConverter
    {
        private static readonly byte[] EntropyBytes = { 0xde, 0xad, 0xbe, 0xef };

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
            byte[] rawData = new byte[secureString.Length * 2];

            try
            {
                valuePtr = Marshal.SecureStringToGlobalAllocUnicode(secureString);
                for (int i = 0; i < secureString.Length * 2; i++)
                {
                    rawData[i] = Marshal.ReadByte(valuePtr, i);
                }

                byte[] protectedData = 
                    ProtectedData.Protect(rawData, EntropyBytes, DataProtectionScope.CurrentUser);

                string encryptedString = Convert.ToBase64String(protectedData);

                writer.WriteValue(encryptedString);
            }
            finally
            {
                Marshal.ZeroFreeGlobalAllocUnicode(valuePtr);

                // Clear the unprotected data array
                for (int i = 0; i < rawData.Length; i++)
                {
                    rawData[i] = 0;
                }
            }
        }

        public override object ReadJson(
            JsonReader reader, 
            Type objectType, 
            object existingValue, 
            JsonSerializer serializer)
        {
            if (reader?.Value == null)
            {
                return serializer.Deserialize(reader, null);
            }

            // ReSharper disable once AssignNullToNotNullAttribute
            byte[] protectedData = Convert.FromBase64String(reader.Value as string);

            byte[] rawData =
                ProtectedData.Unprotect(protectedData, EntropyBytes, DataProtectionScope.CurrentUser);

            try
            {
                SecureString secureString = new SecureString();
                for (int i = 0; i < rawData.Length; i += 2)
                {
                    secureString.AppendChar(BitConverter.ToChar(rawData, i));
                }

                secureString.MakeReadOnly();

                return secureString;
            }
            finally
            {
                // Clear the unprotected data array
                for (int i = 0; i < rawData.Length; i++)
                {
                    rawData[i] = 0;
                }
            }
        }

        public override bool CanConvert(Type objectType)
        {
            throw new NotImplementedException();
        }
    }
}