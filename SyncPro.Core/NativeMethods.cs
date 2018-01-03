namespace SyncPro
{
    using System.Runtime.InteropServices;

    internal static class NativeMethods
    {
        #region msvcrt

        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int memcmp(byte[] b1, byte[] b2, long count);

        public static bool ByteArrayEquals(byte[] b1, byte[] b2)
        {
            // If both buffers are null, then they are equal.
            if (b1 == null && b2 == null)
            {
                return true;
            }

            // If either buffer is null (one is null and one is non-null), they aren't equal.
            if (b1 == null || b2 == null)
            {
                return false;
            }

            // Validate buffers are the same length. This also ensures that the count does not exceed the length of either buffer.
            return b1.Length == b2.Length && memcmp(b1, b2, b1.Length) == 0;
        }

        #endregion
    }
}
