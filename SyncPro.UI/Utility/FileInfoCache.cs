namespace SyncPro.UI.Utility
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Windows;
    using System.Windows.Interop;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;

    public class FileInfo
    {
        public ImageSource SmallIcon { get; set; }

        public ImageSource LargeIcon { get; set; }

        public string DisplayName { get; set; }

        public string TypeName { get; set; }
    }

    public static class FileInfoCache
    {
        private static readonly Dictionary<string, FileInfo> Cache = new Dictionary<string, FileInfo>(StringComparer.OrdinalIgnoreCase);

        private static readonly object CacheLock = new object();

        public static FileInfo GetFileInfo(string extension)
        {
            if (extension == null)
            {
                throw new ArgumentNullException(nameof(extension));
            }

            if (FileInfoCache.Cache.ContainsKey(extension))
            {
                return FileInfoCache.Cache[extension];
            }

            lock (FileInfoCache.CacheLock)
            {
                if (FileInfoCache.Cache.ContainsKey(extension) == false)
                {
                    FileInfo fileInfo = FileInfoCache.InternalGetFileInfo(extension, false, IconSize.Small);
                    FileInfo fileInfo2 = FileInfoCache.InternalGetFileInfo(extension, false, IconSize.Large);

                    fileInfo.LargeIcon = fileInfo2.LargeIcon;

                    FileInfoCache.Cache[extension] = fileInfo;
                }

                return FileInfoCache.Cache[extension];
            }
        }

        public static FileInfo GetFolderInfo()
        {
            return GetFolderIcon(IconSize.Small, FolderType.Closed);
        }

        private enum IconSize
        {
            Large = 0,
            Small = 1,
        }

        private enum FolderType
        {
            Open = 0,
            Closed = 1,
        }

        private static ImageSource ToImageSource(this Icon icon)
        {
            var imageSource = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            return imageSource;
        }

        private static FileInfo InternalGetFileInfo(string name, bool linkOverlay, IconSize size)
        {
            NativeMethods.Shell32.SHFILEINFO shfi = new NativeMethods.Shell32.SHFILEINFO();
            NativeMethods.Shell32.SHGFI flags = NativeMethods.Shell32.SHGFI.Icon |
                                                NativeMethods.Shell32.SHGFI.UseFileAttributes |
                                                NativeMethods.Shell32.SHGFI.DisplayName |
                                                NativeMethods.Shell32.SHGFI.TypeName;

            if (linkOverlay)
            {
                flags |= NativeMethods.Shell32.SHGFI.LinkOverlay;
            }


            /* Check the size specified for return. */
            if (IconSize.Small == size)
            {
                flags |= NativeMethods.Shell32.SHGFI.SmallIcon; // include the small icon flag
            }
            else
            {
                flags |= NativeMethods.Shell32.SHGFI.LargeIcon;  // include the large icon flag
            }

            NativeMethods.Shell32.SHGetFileInfo(
                name,
                NativeMethods.Shell32.FILE_ATTRIBUTE_NORMAL,
                ref shfi,
                (uint)System.Runtime.InteropServices.Marshal.SizeOf(shfi),
                flags);


            // Copy (clone) the returned icon to a new object, thus allowing us 
            // to call DestroyIcon immediately
            System.Drawing.Icon icon = (System.Drawing.Icon)
                                 System.Drawing.Icon.FromHandle(shfi.hIcon).Clone();
            NativeMethods.User32.DestroyIcon(shfi.hIcon); // Cleanup

            FileInfo fileInfo = new FileInfo
            {
                DisplayName = shfi.szDisplayName,
                TypeName = shfi.szTypeName
            };

            if (IconSize.Small == size)
            {
                fileInfo.SmallIcon = icon.ToImageSource();
            }
            else
            {
                fileInfo.LargeIcon = icon.ToImageSource();
            }

            return fileInfo;
        }

        private static FileInfo GetFolderIcon(IconSize size, FolderType folderType)
        {
            // Need to add size check, although errors generated at present!
            NativeMethods.Shell32.SHGFI flags = NativeMethods.Shell32.SHGFI.Icon |
                                                NativeMethods.Shell32.SHGFI.UseFileAttributes;

            if (FolderType.Open == folderType)
            {
                flags |= NativeMethods.Shell32.SHGFI.OpenIcon;
            }

            if (IconSize.Small == size)
            {
                flags |= NativeMethods.Shell32.SHGFI.SmallIcon;
            }
            else
            {
                flags |= NativeMethods.Shell32.SHGFI.LargeIcon;
            }

            // Get the folder icon
            NativeMethods.Shell32.SHFILEINFO shfi = new NativeMethods.Shell32.SHFILEINFO();
            int ret = NativeMethods.Shell32.SHGetFileInfo(
                "empty",
                NativeMethods.Shell32.FILE_ATTRIBUTE_DIRECTORY,
                ref shfi,
                (uint) System.Runtime.InteropServices.Marshal.SizeOf(shfi),
                flags);

            //return new FileInfo();

            System.Drawing.Icon.FromHandle(shfi.hIcon); // Load the icon from an HICON handle

            // Now clone the icon, so that it can be successfully stored in an ImageList
            System.Drawing.Icon icon = (System.Drawing.Icon) System.Drawing.Icon.FromHandle(shfi.hIcon).Clone();

            NativeMethods.User32.DestroyIcon(shfi.hIcon); // Cleanup

            FileInfo fileInfo = new FileInfo
            {
                SmallIcon = icon.ToImageSource(),
                DisplayName = shfi.szDisplayName,
                TypeName = shfi.szTypeName
            };

            return fileInfo;
        }
    }
}
