namespace SyncPro.Utility
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    using Newtonsoft.Json.Linq;

    public static class PathUtility
    {
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
    }

    public class JsonBuilder
    {
        private readonly JObject jObject = new JObject();

        public JsonBuilder AddProperty(string name, string value)
        {
            this.jObject.Add(name, new JValue(value));
            return this;
        }

        public override string ToString()
        {
            return this.jObject.ToString();
        }
    }
}