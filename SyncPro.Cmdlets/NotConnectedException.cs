namespace SyncPro.Cmdlets
{
    using System;
    using System.Runtime.Serialization;

    [Serializable]
    public class NotConnectedException : Exception
    {
        public NotConnectedException()
            : base("You are not currently connected to a running instance of SyncPro. Connect to a running instance " +
                   "with Enter-PSHostProcess, or pass the -Offline parameter to run this command in an offline mode.")
        {
        }

        public NotConnectedException(string message) : base(message)
        {
        }

        public NotConnectedException(string message, Exception inner) : base(message, inner)
        {
        }

        protected NotConnectedException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }
}