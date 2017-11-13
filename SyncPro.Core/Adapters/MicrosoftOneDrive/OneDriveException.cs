namespace SyncPro.Adapters.MicrosoftOneDrive
{
    using System;
    using System.Runtime.Serialization;
    using System.Security;

    /// <summary>
    /// Represents an exception thrown by the OneDrive adapter.
    /// </summary>
    [Serializable]
    public class OneDriveException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OneDriveException"/> class. 
        /// </summary>
        /// <param name="message">
        /// The message that describes the error
        /// </param>
        public OneDriveException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OneDriveException"/> class. 
        /// </summary>
        /// <param name="message">
        /// The message that describes the error
        /// </param>
        /// <param name="innerException">
        /// The exception that is the cause of the current exception
        /// </param>
        public OneDriveException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OneDriveException"/> class with serialization data
        /// </summary>
        /// <param name="info">
        /// The <see cref="SerializationInfo"/> that holds the serialized object data about the exception being thrown.
        /// </param>
        /// <param name="context">
        /// The <see cref="StreamingContext"/> that contains contextual information about the source or destination.
        /// </param>
        protected OneDriveException(SerializationInfo info, StreamingContext context)
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