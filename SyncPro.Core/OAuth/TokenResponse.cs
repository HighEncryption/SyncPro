namespace SyncPro.OAuth
{
    using System;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;

    using Newtonsoft.Json;

    public class TokenResponse
    {
        [JsonProperty("token_type")]
        public string TokenType { get; set; }

        [JsonProperty("expires_in")]
        public int Expires { get; set; }

        [JsonProperty("scopes")]
        public string[] Scopes { get; set; }

        [JsonProperty("access_token")]
        public string AccessToken { get; set; }

        [JsonProperty("refresh_token")]
        public string RefreshToken { get; set; }

        [JsonProperty("id_token")]
        public string IdToken { get; set; }

        [JsonProperty("is_encrypted", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool IsEncrypted { get; set; }

        [JsonProperty("acquire_time")]
        public DateTime AcquireTime { get; set; }

        public void Protect()
        {
            if (this.IsEncrypted)
            {
                return;
            }

            this.AccessToken = TokenHelper.Protect(this.AccessToken);
            this.RefreshToken = TokenHelper.Protect(this.RefreshToken);

            if (!string.IsNullOrWhiteSpace(this.IdToken))
            {
                this.IdToken = TokenHelper.Protect(this.IdToken);
            }

            this.IsEncrypted = true;
        }

        public void Unprotect()
        {
            if (!this.IsEncrypted)
            {
                return;
            }

            this.AccessToken = TokenHelper.Unprotect(this.AccessToken);
            this.RefreshToken = TokenHelper.Unprotect(this.RefreshToken);

            if (!string.IsNullOrWhiteSpace(this.IdToken))
            {
                this.IdToken = TokenHelper.Unprotect(this.IdToken);
            }

            this.IsEncrypted = false;
        }

        public TokenResponse DuplicateToken()
        {
            return new TokenResponse()
            {
                TokenType = this.TokenType,
                Expires = this.Expires,
                Scopes = this.Scopes,
                AccessToken = this.AccessToken,
                RefreshToken = this.RefreshToken,
                IdToken = this.IdToken,
                IsEncrypted = this.IsEncrypted,
                AcquireTime = this.AcquireTime,
            };
        }

        public void SaveProtectedToken(string path)
        {
            TokenResponse duplicateToken = this.DuplicateToken();
            duplicateToken.Protect();

            var tokenContent = JsonConvert.SerializeObject(duplicateToken, Formatting.Indented);
            File.WriteAllText(path, tokenContent);
        }

        public string GetAccessTokenHash()
        {
            var input = Encoding.ASCII.GetBytes(this.AccessToken);
            using (var sha1 = new SHA1CryptoServiceProvider())
            {
                var output = sha1.ComputeHash(input);
                return BitConverter.ToString(output).Replace("-", "");
            }
        }

        public string GetRefreshTokenHash()
        {
            var input = Encoding.ASCII.GetBytes(this.AccessToken);
            using (var sha1 = new SHA1CryptoServiceProvider())
            {
                var output = sha1.ComputeHash(input);
                return BitConverter.ToString(output).Replace("-", "");
            }
        }
    }
}