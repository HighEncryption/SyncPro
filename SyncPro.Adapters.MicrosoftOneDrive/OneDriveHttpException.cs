namespace SyncPro.Adapters.MicrosoftOneDrive
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Runtime.Serialization;
    using System.Security;

    /// <summary>
    /// Represents an exception thrown by the OneDrive adapter.
    /// </summary>
    [Serializable]
    public class OneDriveHttpException : Exception
    {
        public int HttpStatusCode { get; set; }

        public string HttpStatusMessage { get; set; }

        public OneDriveErrorResponse ErrorResponse { get; set; }

        public Dictionary<string, IList<string>> ResponseHeaders { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OneDriveHttpException"/> class. 
        /// </summary>
        /// <param name="message">
        /// The message that describes the error
        /// </param>
        public OneDriveHttpException(string message)
            : base(message)
        {
            this.ResponseHeaders = new Dictionary<string, IList<string>>();
        }

        public OneDriveHttpException(string message, HttpStatusCode statusCode)
            : base(message)
        {
            this.ResponseHeaders = new Dictionary<string, IList<string>>();

            this.HttpStatusCode = (int)statusCode;
            this.HttpStatusMessage = Enum.GetName(typeof(HttpStatusCode), statusCode);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OneDriveHttpException"/> class. 
        /// </summary>
        /// <param name="message">
        /// The message that describes the error
        /// </param>
        /// <param name="innerException">
        /// The exception that is the cause of the current exception
        /// </param>
        public OneDriveHttpException(string message, Exception innerException)
            : base(message, innerException)
        {
            this.ResponseHeaders = new Dictionary<string, IList<string>>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OneDriveHttpException"/> class with serialization data
        /// </summary>
        /// <param name="info">
        /// The <see cref="SerializationInfo"/> that holds the serialized object data about the exception being thrown.
        /// </param>
        /// <param name="context">
        /// The <see cref="StreamingContext"/> that contains contextual information about the source or destination.
        /// </param>
        protected OneDriveHttpException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            if (info == null)
            {
                throw new ArgumentNullException("info");
            }

            this.ResponseHeaders = new Dictionary<string, IList<string>>();
        }

        /// <summary>
        /// Gets the <see cref="SerializationInfo"/> with information about the exception.
        /// </summary>
        /// <param name="info">
        /// The <see cref="SerializationInfo"/> that holds the serialized object data 
        /// about the exception being thrown.
        /// </param>
        /// <param name="context">
        /// The <see cref="StreamingContext"/> that contains contextual information 
        /// about the source or destination.
        /// </param>
        [SecurityCritical]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
            {
                throw new ArgumentNullException("info");
            }

            base.GetObjectData(info, context);
        }

        public static Exception FromResponse(HttpResponseMessage response)
        {
            string exceptionMessage = string.Format("The OneDrive service returned {0} ({1})", (int) response.StatusCode,
                response.ReasonPhrase);

            OneDriveErrorResponseContainer errorResponse = response.Content.TryReadAsJsonAsync<OneDriveErrorResponseContainer>().Result;

            var exception = new OneDriveHttpException(exceptionMessage, response.StatusCode);
            if (errorResponse != null)
            {
                exception.ErrorResponse = errorResponse.ErrorResponse;
            }

            foreach (KeyValuePair<string, IEnumerable<string>> responseHeader in response.Headers)
            {
                exception.ResponseHeaders.Add(responseHeader.Key, new List<string>(responseHeader.Value));
            }

            return exception;
        }
    }
}