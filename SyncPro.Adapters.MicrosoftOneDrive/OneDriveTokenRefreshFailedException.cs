namespace SyncPro.Adapters.MicrosoftOneDrive
{
    using System;
    using System.Net;
    using System.Runtime.Serialization;
    using System.Security;

    /// <summary>
    /// Represents an exception thrown by the OneDrive adapter.
    /// </summary>
    [Serializable]
    public class OneDriveTokenRefreshFailedException : Exception
    {
        public WindowsLiveError ErrorData { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OneDriveTokenRefreshFailedException"/> class. 
        /// </summary>
        /// <param name="message">
        /// The message that describes the error
        /// </param>
        public OneDriveTokenRefreshFailedException(string message)
            : base(message)
        {
        }

        public OneDriveTokenRefreshFailedException(string message, WindowsLiveError errorData)
            : base(message)
        {
            this.ErrorData = errorData;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OneDriveTokenRefreshFailedException"/> class. 
        /// </summary>
        /// <param name="message">
        /// The message that describes the error
        /// </param>
        /// <param name="innerException">
        /// The exception that is the cause of the current exception
        /// </param>
        public OneDriveTokenRefreshFailedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OneDriveTokenRefreshFailedException"/> class with serialization data
        /// </summary>
        /// <param name="info">
        /// The <see cref="SerializationInfo"/> that holds the serialized object data about the exception being thrown.
        /// </param>
        /// <param name="context">
        /// The <see cref="SerializationInfo"/> that contains contextual information about the source or destination.
        /// </param>
        protected OneDriveTokenRefreshFailedException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            if (info == null)
            {
                throw new ArgumentNullException("info");
            }
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
    }

}