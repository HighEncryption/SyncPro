namespace SyncPro
{
    public enum SyncScopeType
    {
        /// <summary>
        /// The sync scope is undefined
        /// </summary>
        Undefined = 0,

        /// <summary>
        /// Files are only synchronized from the source to the destination
        /// </summary>
        SourceToDestination = 1,

        /// <summary>
        /// Files are only synchronized from the destination to the source
        /// </summary>
        DestinationToSource = 2,

        /// <summary>
        /// Files are synchronized from the source to the destination and from the destination to the source
        /// </summary>
        Bidirectional = 3
    }
}