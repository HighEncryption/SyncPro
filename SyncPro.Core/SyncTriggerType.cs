namespace SyncPro
{
    public enum SyncTriggerType
    {
        /// <summary>
        /// The trigger type is undefined
        /// </summary>
        Undefined,

        /// <summary>
        /// The sync is triggered whenever a change occurs
        /// </summary>
        Continuous,

        /// <summary>
        /// The sync occurs at a scheduled time
        /// </summary>
        Scheduled,

        /// <summary>
        /// The sync occurs only when manually triggered
        /// </summary>
        Manual,

        /// <summary>
        /// The sync occurs when a specific device is inserted
        /// </summary>
        DeviceInsertion,
    }
}