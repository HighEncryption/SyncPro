namespace SyncPro.Cmdlets
{
    using System;
    using System.Runtime.Serialization;

    [Serializable]
    public class NoSelectedRelationshipException : Exception
    {
        public NoSelectedRelationshipException()
            : base("No relationship is selected in the UI. Either select a relationship or include the " +
                   "RelationshipId parameter to selected a relationship.")
        {
        }

        public NoSelectedRelationshipException(string message) : base(message)
        {
        }

        public NoSelectedRelationshipException(string message, Exception inner) : base(message, inner)
        {
        }

        protected NoSelectedRelationshipException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }
}