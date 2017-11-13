////namespace SyncPro.Adapters.MicrosoftOneDrive
////{
////    using System;
////    using System.IO;
////    using System.Security.Cryptography;
////    using System.Text;

////    using Newtonsoft.Json;

////    public class TokenResponse
////    {
////        [JsonProperty("token_type")]
////        public string TokenType { get; set; }

////        [JsonProperty("expires_in")]
////        public int Expires { get; set; }

////        [JsonProperty("scope")]
////        public string Scope { get; set; }

////        [JsonProperty("access_token")]
////        public string AccessToken { get; set; }

////        [JsonProperty("refresh_token")]
////        public string RefreshToken { get; set; }

////        [JsonProperty("is_encrypted", DefaultValueHandling = DefaultValueHandling.Ignore)]
////        public bool IsEncrypted { get; set; }

////        [JsonProperty("acquire_time")]
////        public DateTime AcquireTime { get; set; }

////        public void Protect()
////        {
////            if (this.IsEncrypted)
////            {
////                return;
////            }

////            this.AccessToken = OneDriveAdapter.Protect(this.AccessToken);
////            this.RefreshToken = OneDriveAdapter.Protect(this.RefreshToken);
////            this.IsEncrypted = true;
////        }

////        public void Unprotect()
////        {
////            if (!this.IsEncrypted)
////            {
////                return;
////            }

////            this.AccessToken = OneDriveAdapter.Unprotect(this.AccessToken);
////            this.RefreshToken = OneDriveAdapter.Unprotect(this.RefreshToken);
////            this.IsEncrypted = true;
////        }

////        public TokenResponse DuplicateToken()
////        {
////            return new TokenResponse()
////            {
////                TokenType = this.TokenType,
////                Expires = this.Expires,
////                Scope = this.Scope,
////                AccessToken = this.AccessToken,
////                RefreshToken = this.RefreshToken,
////                IsEncrypted = this.IsEncrypted,
////            };
////        }

////        public void SaveProtectedToken(string path)
////        {
////            TokenResponse duplicateToken = this.DuplicateToken();
////            duplicateToken.Protect();

////            var tokenContent = JsonConvert.SerializeObject(duplicateToken, Formatting.Indented);
////            File.WriteAllText(path, tokenContent);
////        }

////        public string GetAccessTokenHash()
////        {
////            var input = Encoding.ASCII.GetBytes(this.AccessToken);
////            using (var sha1 = new SHA1CryptoServiceProvider())
////            {
////                var output = sha1.ComputeHash(input);
////                return BitConverter.ToString(output).Replace("-", "");
////            }
////        }

////        public string GetRefreshTokenHash()
////        {
////            var input = Encoding.ASCII.GetBytes(this.AccessToken);
////            using (var sha1 = new SHA1CryptoServiceProvider())
////            {
////                var output = sha1.ComputeHash(input);
////                return BitConverter.ToString(output).Replace("-", "");
////            }
////        }
////    }
////}