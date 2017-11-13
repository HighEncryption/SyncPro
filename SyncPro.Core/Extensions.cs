namespace SyncPro
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public static class DictionaryExtensions
    {
        public static TValue GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key)
        {
            TValue value;
            if (dictionary.TryGetValue(key, out value))
            {
                return value;
            }

            return default(TValue);
        }
    }

    public static class UriExtensions
    {
        public static Dictionary<string, string> GetQueryParameters(this Uri uri)
        {
            if (string.IsNullOrEmpty(uri.Query) || uri.Query == "?")
            {
                return new Dictionary<string, string>();
            }

            // Trim the '?' from the start of the string, split at '&' into individual parameters, then split parameters 
            // at '=' to produce the name and value of each parameter;
            return uri.Query.TrimStart('?')
                .Split('&')
                .Select(e => e.Split('='))
                .ToDictionary(e => e[0], e => HttpUtility.UrlDecode(e[1]), StringComparer.OrdinalIgnoreCase);
        }

        public static Uri ReplaceQueryParameterIfExists(this Uri uri, string name, string value)
        {
            if (string.IsNullOrEmpty(uri.Query) || uri.Query == "?")
            {
                return uri;
            }

            Dictionary<string, string> queryParams = uri.GetQueryParameters();

            if (!queryParams.ContainsKey(name))
            {
                return uri;
            }

            queryParams[name] = value;

            UriBuilder builder = new UriBuilder(uri);
            builder.Query = string.Join("&", queryParams.Select(param => param.Key + "=" + param.Value));

            return builder.Uri;
        }

        public static string CombineQueryString(ICollection<KeyValuePair<string, string>> parameter)
        {
            if (!parameter.Any())
            {
                return string.Empty;
            }

            return "?" + string.Join("&", parameter.Select(p => p.Key + "=" + p.Value));
        }
    }

    public static class StringExtensions
    {
        private static readonly string[] UndefinedFlagNames = { "undefined" };

        public static IEnumerable<string> GetSetFlagNames<TEnum>(object value)
        {
            Type enumType = typeof(TEnum);
            if (!enumType.IsEnum)
            {
                throw new InvalidOperationException("Type " + enumType.FullName + " is not a Enum type.");
            }

            List<string> set = new List<string>();
            foreach (object val in Enum.GetValues(enumType))
            {
                if ((Convert.ToUInt64(value) & Convert.ToUInt64(val)) != 0)
                {
                    string name = Enum.GetName(enumType, val);
                    if (!UndefinedFlagNames.All(n => n.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    {
                        set.Add(name);
                    }
                }
            }

            return set;
        }
    }

    public static class HttpContentExtensions
    {
        public static async Task<T> ReadAsJsonAsync<T>(this HttpContent content)
        {
            JsonSerializer serializer = new JsonSerializer();
            using (var stream = await content.ReadAsStreamAsync().ConfigureAwait(false))
            {
                return serializer.Deserialize<T>(new JsonTextReader(new StreamReader(stream)));
            }
        }

        public static async Task<T> TryReadAsJsonAsync<T>(this HttpContent content)
            where T : class
        {
            try
            {
                JsonSerializer serializer = new JsonSerializer();
                using (var stream = await content.ReadAsStreamAsync().ConfigureAwait(false))
                {
                    return serializer.Deserialize<T>(new JsonTextReader(new StreamReader(stream)));
                }
            }
            catch
            {
                return null;
            }
        }

        public static async Task<JObject> ReadAsJObjectAsync(this HttpContent content)
        {
            using (var stream = await content.ReadAsStreamAsync().ConfigureAwait(false))
            {
                return JObject.Load(new JsonTextReader(new StreamReader(stream)));
            }
        }
    }

    public static class HttpRequestMessageExtensions
    {
        public static async Task<HttpRequestMessage> Clone(this HttpRequestMessage request)
        {
            HttpRequestMessage newRequest = new HttpRequestMessage(request.Method, request.RequestUri);

            // Copy the request's content (via a MemoryStream) into the cloned object
            if (request.Content != null)
            {
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    await request.Content.CopyToAsync(memoryStream).ConfigureAwait(false);
                    memoryStream.Position = 0;
                    newRequest.Content = new StreamContent(memoryStream);

                    // Copy the content headers
                    if (request.Content.Headers != null)
                    {
                        foreach (KeyValuePair<string, IEnumerable<string>> header in request.Content.Headers)
                        {
                            newRequest.Content.Headers.Add(header.Key, header.Value);
                        }
                    }
                }
            }

            newRequest.Version = request.Version;

            foreach (KeyValuePair<string, object> property in request.Properties)
            {
                newRequest.Properties.Add(property);
            }

            foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers)
            {
                newRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return newRequest;
        }
    }

    public static class TaskExtensions
    {
        public static Task ThrowIfFaulted(this Task task)
        {
            return task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    throw new Exception("Failed failed", t.Exception);
                }
            });
        }
    }
}