namespace SyncPro.Cmdlets
{
    using System;
    using System.Runtime.Serialization;

    [Serializable]
    public class RelationshipNotFoundException : Exception
    {
        public RelationshipNotFoundException()
        {
        }

        public RelationshipNotFoundException(string message) : base(message)
        {
        }

        public RelationshipNotFoundException(string message, Exception inner) : base(message, inner)
        {
        }

        protected RelationshipNotFoundException(
            SerializationInfo info,
            StreamingContext context)
            : base(info, context)
        {
        }
    }
}