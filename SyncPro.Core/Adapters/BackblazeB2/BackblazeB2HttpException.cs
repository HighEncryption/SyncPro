namespace SyncPro.Adapters.BackblazeB2
{
    using System;
    using System.Net.Http;
    using System.Runtime.Serialization;

    /// <summary>
    /// Represents an error returned by the Backblaze B2 API. See https://www.backblaze.com/b2/docs/calling.html#error_handling
    /// </summary>
    [Serializable]
    public class BackblazeB2HttpException : Exception
    {
        public int Status { get; set; }

        public string Code { get; set; }

        public BackblazeB2HttpException()
        {
        }

        public BackblazeB2HttpException(
            string message,
            int status,
            string code) 
            : base(message)
        {
            this.Status = status;
            this.Code = code;
        }

        protected BackblazeB2HttpException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }

        public BackblazeB2HttpException(BackblazeErrorResponse error)
            : this(error.Message, error.Status, error.Code)
        {
        }
    }
}