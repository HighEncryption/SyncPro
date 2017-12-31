namespace SyncPro
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
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

            // Copy the request's content (via a MemoryStream) into the cloned object. Note that the MemoryStream
            // is not disposed here because it needs to be assigned to the new request (and will be disposed of 
            // after the request has been sent.
            if (request.Content != null)
            {
                MemoryStream memoryStream = new MemoryStream();

                // Copy the content from the original request. Note that the HttpClient normally disposes of the
                // content stream upon sending. The DelayedDispose*Content classes prevent the immediate diposing
                // of the underlying stream, allowing it to be re-read here.
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

        public static void DisposeCustomContent(this HttpRequestMessage request)
        {
            try
            {
                IDelayedDisposeContent delayedDisposeContent = request.Content as IDelayedDisposeContent;
                delayedDisposeContent?.DelayedDispose();
            }
            catch (ObjectDisposedException)
            {
                // Suppress object disposed exception for cases where the request or the request's content 
                // has already been disposed.
            }
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

    public static class StreamExtensions
    {
        public static short ReadInt16(this Stream stream)
        {
            byte[] bytes = new byte[2];
            stream.Read(bytes, 0, 2);
            return BitConverter.ToInt16(bytes, 0);
        }

        public static int ReadInt32(this Stream stream)
        {
            byte[] bytes = new byte[4];
            stream.Read(bytes, 0, 4);
            return BitConverter.ToInt32(bytes, 0);
        }

        public static long ReadInt64(this Stream stream)
        {
            byte[] bytes = new byte[8];
            stream.Read(bytes, 0, 8);
            return BitConverter.ToInt64(bytes, 0);
        }

        public static byte[] ReadByteArray(this Stream stream, int length, int startIndex)
        {
            byte[] bytes = new byte[length];
            stream.Read(bytes, startIndex, length);
            return bytes;
        }
    }

    public interface IDelayedDisposeContent
    {
        void DelayedDispose();
    }

    public class DelayedDisposeStringContent : StringContent, IDelayedDisposeContent
    {
        public DelayedDisposeStringContent(string content)
            : base(content)
        {
        }

        public DelayedDisposeStringContent(string content, Encoding encoding)
            : base(content, encoding)
        {
        }

        public DelayedDisposeStringContent(string content, Encoding encoding, string mediaType)
            : base(content, encoding, mediaType)
        {
        }

        protected override void Dispose(bool disposing)
        {
            // Do not dispose of resources normally
        }

        public void DelayedDispose()
        {
            base.Dispose(true);
        }
    }

    public class DelayedDisposeStreamContent : StreamContent, IDelayedDisposeContent
    {
        public DelayedDisposeStreamContent(Stream content)
            : base(content)
        {
        }

        public DelayedDisposeStreamContent(Stream content, int bufferSize)
            : base(content, bufferSize)
        {
        }

        protected override void Dispose(bool disposing)
        {
            // Do not dispose of resources normally
        }

        public void DelayedDispose()
        {
            base.Dispose(true);
        }
    }
}