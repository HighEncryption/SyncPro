namespace SyncPro.Utility
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    using Newtonsoft.Json.Linq;

    public static class PathUtility
    {
        private static readonly char[] pathSeparators = new[] {'\\'};

        public static string Join(string pathSeparator, IList<string> values)
        {
            int length = values.Sum(v => v.Length) + (values.Count - 1) * pathSeparator.Length;

            if (length < 3)
            {
                length = 3;
            }

            StringBuilder sb = new StringBuilder(length);

            using (IEnumerator<string> enumerator = values.GetEnumerator())
            {
                if (!enumerator.MoveNext())
                {
                    return string.Empty;
                }

                if (enumerator.Current != null)
                {
                    sb.Append(enumerator.Current.Trim('\\'));
                }

                while (enumerator.MoveNext())
                {
                    sb.Append(pathSeparator);
                    if (enumerator.Current != null)
                    {
                        sb.Append(enumerator.Current.Trim('\\'));
                    }
                }

                if (sb.Length == 2 && char.IsLetter(sb[0]) && sb[1] == ':')
                {
                    sb.Append(pathSeparator);
                }

                return sb.ToString();
            }
        }

        public static string TrimStart(string path, int count)
        {
            string[] segments = path.Split(PathUtility.pathSeparators);
            return string.Join("\\", segments, count, segments.Length - count);
        }

        public static string GetSegment(string path, int index)
        {
            string[] parts = path.Split(PathUtility.pathSeparators);

            if (index >= 0)
            {
                if (index > parts.Length - 1)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(index),
                        "index is greater than the number of parts in the path");
                }

                return parts[index];
            }

            if (0 - index > parts.Length - 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(index));
            }

            return parts[parts.Length + index];
        }
    }

    public class JsonBuilder
    {
        private readonly JObject jObject = new JObject();

        public JsonBuilder AddProperty(string name, string value)
        {
            this.jObject.Add(name, new JValue(value));
            return this;
        }

        public JsonBuilder AddArrayProperty(string name, object value)
        {
            this.jObject.Add(name, JArray.FromObject(value));
            return this;
        }

        public override string ToString()
        {
            return this.jObject.ToString();
        }
    }
}