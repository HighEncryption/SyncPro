namespace SyncPro.Adapters.WindowsFileSystem
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Runtime.InteropServices;

    using Microsoft.Win32.SafeHandles;

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    internal static class NativeMethods
    {
        #region Kernel32

        internal const int GENERIC_READ = unchecked((int) 0x80000000);
        internal const int FILE_FLAG_BACKUP_SEMANTICS = unchecked((int) 0x02000000);
        internal const int OPEN_EXISTING = unchecked((int)3);

        internal const uint FSCTL_GET_OBJECT_ID = 0x0009009c;
        internal const uint FSCTL_CREATE_OR_GET_OBJECT_ID = 0x000900c0;

        public struct BY_HANDLE_FILE_INFORMATION
        {
            public uint FileAttributes;
#pragma warning disable 618
            public FILETIME CreationTime;
            public FILETIME LastAccessTime;
            public FILETIME LastWriteTime;
#pragma warning restore 618
            public uint VolumeSerialNumber;
            public uint FileSizeHigh;
            public uint FileSizeLow;
            public uint NumberOfLinks;
            public uint FileIndexHigh;
            public uint FileIndexLow;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct FILE_OBJECTID_BUFFER
        {
            public struct Union
            {
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
                public byte[] BirthVolumeId;

                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
                public byte[] BirthObjectId;

                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
                public byte[] DomainId;
            }

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] ObjectId;

            public Union BirthInfo;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 48)]
            public byte[] ExtendedInfo;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetFileInformationByHandle(IntPtr hFile, out BY_HANDLE_FILE_INFORMATION lpFileInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern SafeFileHandle CreateFile(
            string lpFileName,
            int dwDesiredAccess,
            FileShare dwShareMode,
            IntPtr lpSecurityAttributes,
            FileMode dwCreationDisposition,
            int dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool DeviceIoControl(
            SafeFileHandle hDevice,
            uint dwIoControlCode,
            IntPtr lpInBuffer,
            uint nInBufferSize,
            [Out] IntPtr lpOutBuffer,
            int nOutBufferSize,
            ref uint lpBytesReturned,
            IntPtr lpOverlapped);

        #endregion

        //#region msvcrt

        //[DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        //public static extern int memcmp(byte[] b1, byte[] b2, long count);

        //public static bool ByteArrayEquals(byte[] b1, byte[] b2)
        //{
        //    // If both buffers are null, then they are equal.
        //    if (b1 == null && b2 == null)
        //    {
        //        return true;
        //    }

        //    // If either buffer is null (one is null and one is non-null), they aren't equal.
        //    if (b1 == null || b2 == null)
        //    {
        //        return false;
        //    }

        //    // Validate buffers are the same length. This also ensures that the count does not exceed the length of either buffer.
        //    return b1.Length == b2.Length && memcmp(b1, b2, b1.Length) == 0;
        //}

        //#endregion

    }
}