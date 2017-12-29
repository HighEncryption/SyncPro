namespace SyncPro.Adapters.WindowsFileSystem
{
    using System;
    using System.ComponentModel;
    using System.IO;
    using System.Runtime.InteropServices;

    using Microsoft.Win32.SafeHandles;

    internal sealed class NativeMethodHelpers
    {
        public static byte[] GetDirectoryObjectId(string path)
        {
            SafeFileHandle handle = null;
            IntPtr outBuffer = IntPtr.Zero;

            try
            {
                // Get a handle to the directory
                handle = NativeMethods.CreateFile(
                    path,
                    NativeMethods.GENERIC_READ, 
                    FileShare.Read,
                    IntPtr.Zero,
                    (FileMode) NativeMethods.OPEN_EXISTING,
                    NativeMethods.FILE_FLAG_BACKUP_SEMANTICS,
                    IntPtr.Zero);

                if (null == handle || handle.IsInvalid)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                // Get the size of the buffer object
                NativeMethods.FILE_OBJECTID_BUFFER buffer = default(NativeMethods.FILE_OBJECTID_BUFFER);
                int bufferSize = Marshal.SizeOf(buffer);

                // Allocate unmanaged memory for the DeviceIoContol() method to write the buffer
                outBuffer = Marshal.AllocHGlobal(bufferSize);
                uint bytesReturned = default(uint);

                // Call DeviceIoControl() to read the object id
                bool controlResult = NativeMethods.DeviceIoControl(
                        handle, 
                        NativeMethods.FSCTL_CREATE_OR_GET_OBJECT_ID,
                        IntPtr.Zero, 
                        0,
                        outBuffer, 
                        bufferSize,
                        ref bytesReturned, 
                        IntPtr.Zero);

                if (!controlResult)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                // Convert unmanged to managed memory structure
                buffer = (NativeMethods.FILE_OBJECTID_BUFFER) Marshal.PtrToStructure(
                    outBuffer, typeof(NativeMethods.FILE_OBJECTID_BUFFER));

                // Copy the object ID into a dedicated byte array that can be returned
                byte[] objectId = new byte[buffer.ObjectId.Length];
                buffer.ObjectId.CopyTo(objectId, 0);

                return objectId;
            }
            finally 
            {
                if (outBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(outBuffer);
                }

                if (handle != null && !handle.IsInvalid && !handle.IsClosed)
                {
                    handle.Close();
                }
            }
        }

        public static byte[] GetFileObjectId(string path)
        {

            FileInfo fi = new FileInfo(path);
            FileStream fs = null;

            try
            {
                NativeMethods.BY_HANDLE_FILE_INFORMATION objectFileInfo;

                fs = fi.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

#pragma warning disable 618
                NativeMethods.GetFileInformationByHandle(fs.Handle, out objectFileInfo);
#pragma warning restore 618

                ulong fileIndex = ((ulong)objectFileInfo.FileIndexHigh << 32) + objectFileInfo.FileIndexLow;

                return BitConverter.GetBytes(fileIndex);
            }
            finally
            {
                if (fs != null)
                {
                    fs.Dispose();
                }
            }
        }
    }
}