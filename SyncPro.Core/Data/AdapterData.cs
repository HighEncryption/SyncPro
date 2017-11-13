namespace SyncPro.Data
{
    using System;

    [Flags]
    public enum AdapterFlags : short
    {
        None = 0x00,
        Originator = 0x01
    }
}