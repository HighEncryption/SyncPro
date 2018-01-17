namespace SyncPro.Tracing
{
    using System;
    using System.Text;

    using JetBrains.Annotations;

    public static class StringBuilderExtensions
    {
        [StringFormatMethod("format")]
        public static StringBuilder AppendLineFeed(this StringBuilder stringBuilder, string format, params object[] args)
        {
            return stringBuilder.Append(
                string.Format(format + "\n", args));
        }

        [StringFormatMethod("format")]
        public static StringBuilder AppendLine(this StringBuilder stringBuilder, string format, params object[] args)
        {
            return stringBuilder.Append(
                string.Format(format + Environment.NewLine, args));
        }
    }
}