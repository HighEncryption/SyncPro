namespace SyncPro.Adapters.BackblazeB2
{
    using System;
    using System.Runtime.Serialization;

    [Serializable]
    public class UploadException : Exception
    {
        public UploadException()
        {
        }

        public UploadException(string message) : base(message)
        {
        }

        public UploadException(string message, Exception inner) : base(message, inner)
        {
        }

        protected UploadException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }
}