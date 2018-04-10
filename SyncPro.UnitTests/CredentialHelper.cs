namespace SyncPro.UnitTests
{
    using System;
    using System.ComponentModel;
    using System.Runtime.InteropServices;
    using System.Security;
    using System.Text;

    public static class CredentialHelper
    {
        public static CredentialResult PromptForCredentials(string message)
        {
            StringBuilder userPassword = new StringBuilder(100);
            StringBuilder userID = new StringBuilder(100);
            NativeMethods.CredUI.CREDUI_INFO credUi = new NativeMethods.CredUI.CREDUI_INFO();
            credUi.cbSize = Marshal.SizeOf(credUi);
            credUi.pszMessageText = message;
            bool save = false;

            NativeMethods.CredUI.CREDUI_FLAGS flags = 
                NativeMethods.CredUI.CREDUI_FLAGS.ALWAYS_SHOW_UI | 
                NativeMethods.CredUI.CREDUI_FLAGS.GENERIC_CREDENTIALS | 
                NativeMethods.CredUI.CREDUI_FLAGS.SHOW_SAVE_CHECK_BOX;

            NativeMethods.CredUI.CredUIReturnCodes returnCode =
                NativeMethods.CredUI.CredUIPromptForCredentials(
                    ref credUi,
                    "localhost", 
                    IntPtr.Zero, 
                    0, 
                    userID, 
                    100, 
                    userPassword, 
                    100, 
                    ref save, 
                    flags);

            if (returnCode == NativeMethods.CredUI.CredUIReturnCodes.NO_ERROR)
            {
                SecureString secureString = new SecureString();
                foreach (char c in userPassword.ToString())
                {
                    secureString.AppendChar(c);
                }

                secureString.MakeReadOnly();

                return new CredentialResult
                {
                    Username = userID.ToString(),
                    Password = secureString
                };
            }

            if (Enum.IsDefined(typeof(NativeMethods.CredUI.CredUIReturnCodes), returnCode))
            {
                string errorName = Enum.GetName(typeof(NativeMethods.CredUI.CredUIReturnCodes), returnCode);
                throw new Win32Exception((int)returnCode, "Prompt for credentials failed with " + errorName);
            }

            throw new Win32Exception((int)returnCode);
        }
    }

    public class CredentialResult
    {
        public string Username { get; set; }

        public SecureString Password { get; set; }
    }

    internal static class NativeMethods
    {
        // ReSharper disable InconsistentNaming

        public static class CredUI
        {
            [Flags]
            public enum CREDUI_FLAGS
            {
                INCORRECT_PASSWORD = 0x1,
                DO_NOT_PERSIST = 0x2,
                REQUEST_ADMINISTRATOR = 0x4,
                EXCLUDE_CERTIFICATES = 0x8,
                REQUIRE_CERTIFICATE = 0x10,
                SHOW_SAVE_CHECK_BOX = 0x40,
                ALWAYS_SHOW_UI = 0x80,
                REQUIRE_SMARTCARD = 0x100,
                PASSWORD_ONLY_OK = 0x200,
                VALIDATE_USERNAME = 0x400,
                COMPLETE_USERNAME = 0x800,
                PERSIST = 0x1000,
                SERVER_CREDENTIAL = 0x4000,
                EXPECT_CONFIRMATION = 0x20000,
                GENERIC_CREDENTIALS = 0x40000,
                USERNAME_TARGET_CREDENTIALS = 0x80000,
                KEEP_USERNAME = 0x100000,
            }

            public enum CredUIReturnCodes
            {
                NO_ERROR = 0,
                ERROR_CANCELLED = 1223,
                ERROR_NO_SUCH_LOGON_SESSION = 1312,
                ERROR_NOT_FOUND = 1168,
                ERROR_INVALID_ACCOUNT_NAME = 1315,
                ERROR_INSUFFICIENT_BUFFER = 122,
                ERROR_INVALID_PARAMETER = 87,
                ERROR_INVALID_FLAGS = 1004,
                ERROR_BAD_ARGUMENTS = 160
            }

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            public struct CREDUI_INFO
            {
                public int cbSize;
                public IntPtr hwndParent;
                public string pszMessageText;
                public string pszCaptionText;
                public IntPtr hbmBanner;
            }

            [DllImport("credui", CharSet = CharSet.Unicode)]
            public static extern CredUIReturnCodes CredUIPromptForCredentials(
                ref CREDUI_INFO creditUR,
                string targetName,
                IntPtr reserved1,
                int iError,
                StringBuilder userName,
                int maxUserName,
                StringBuilder password,
                int maxPassword,
                [MarshalAs(UnmanagedType.Bool)] ref bool pfSave,
                CREDUI_FLAGS flags);
        }

        // ReSharper restore InconsistentNaming
    }
}
