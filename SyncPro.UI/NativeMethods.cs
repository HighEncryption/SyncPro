namespace SyncPro.UI
{
    using System;
    using System.Runtime.InteropServices;

    internal static class NativeMethods
    {
        // ReSharper disable InconsistentNaming
        #region User32

        internal class User32
        {
            public const int WM_SYSCOMMAND = 0x112;

            [DllImport("user32.dll")]
            public static extern bool SetForegroundWindow(IntPtr hWnd);

            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            public static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool DestroyIcon(IntPtr hIcon);
        }

        #endregion

        #region Shell32

        internal class Shell32
        {
            public const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
            public const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;

            [Flags]
            public enum SHGFI : uint
            {
                Icon = 0x000000100,
                DisplayName = 0x000000200,
                TypeName = 0x000000400,
                Attributes = 0x000000800,
                IconLocation = 0x000001000,
                ExeType = 0x000002000,
                SysIconIndex = 0x000004000,
                LinkOverlay = 0x000008000,
                Selected = 0x000010000,
                Attr_Specified = 0x000020000,
                LargeIcon = 0x000000000,
                SmallIcon = 0x000000001,
                OpenIcon = 0x000000002,
                ShellIconSize = 0x000000004,
                PIDL = 0x000000008,
                UseFileAttributes = 0x000000010,
                AddOverlays = 0x000000020,
                OverlayIndex = 0x000000040,
            }

            [DllImport("shell32.dll", CharSet = CharSet.Auto)]
            public static extern int SHGetFileInfo(
              string pszPath,
              uint dwFileAttributes,
              ref SHFILEINFO psfi,
              uint cbfileInfo,
              SHGFI uFlags);

            /// <summary>Maximal Length of unmanaged Windows-Path-strings</summary>
            public const int MAX_PATH = 260;

            /// <summary>Maximal Length of unmanaged Typename</summary>
            public const int MAX_TYPE = 80;

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
            public struct SHFILEINFO
            {
                public SHFILEINFO(bool b)
                {
                    this.hIcon = IntPtr.Zero;
                    this.iIcon = 0;
                    this.dwAttributes = 0;
                    this.szDisplayName = "";
                    this.szTypeName = "";
                }
                public IntPtr hIcon;
                public int iIcon;
                public uint dwAttributes;
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = Shell32.MAX_PATH)]
                public string szDisplayName;
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = Shell32.MAX_TYPE)]
                public string szTypeName;
            };
        }



        #endregion

        #region gdi32

        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern bool DeleteObject(IntPtr hObject);

        #endregion
        // ReSharper restore InconsistentNaming
    }
}