namespace SyncPro.Adapters.BackblazeB2
{
    using System;
    using System.Runtime.Serialization;

    [Serializable]
    public class UploadCancelledException : Exception
    {
        public UploadCancelledException()
        {
        }

        public UploadCancelledException(string message) : base(message)
        {
        }

        public UploadCancelledException(string message, Exception inner) : base(message, inner)
        {
        }

        protected UploadCancelledException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }
}