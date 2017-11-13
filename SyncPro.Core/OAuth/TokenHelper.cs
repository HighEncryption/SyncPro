namespace SyncPro.OAuth
{
    using System;
    using System.Security.Cryptography;
    using System.Text;

    public class TokenRefreshedEventArgs : EventArgs
    {
        public TokenResponse NewToken { get; set; }
    }

    public class TokenHelper
    {
        private static readonly byte[] EntropyBytes = { 0xde, 0xad, 0xbe, 0xef };

        internal static string Protect(string value)
        {
            byte[] rawData = Encoding.UTF8.GetBytes(value);
            byte[] encData = ProtectedData.Protect(rawData, EntropyBytes, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encData);
        }

        internal static string Unprotect(string value)
        {
            byte[] encData = Convert.FromBase64String(value);
            byte[] rawData = ProtectedData.Unprotect(encData, EntropyBytes, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(rawData);
        }
    }
}