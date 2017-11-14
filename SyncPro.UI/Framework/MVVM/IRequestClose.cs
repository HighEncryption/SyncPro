namespace SyncPro.UI.Framework.MVVM
{
    using System;
    using System.ComponentModel;

    public delegate void RequestCloseEventHandler(object sender, RequestCloseEventArgs e);

    public interface IRequestClose
    {
        event RequestCloseEventHandler RequestClose;

        void WindowClosing(CancelEventArgs e);

        bool MustClose { get; set; }
    }

    public class RequestCloseEventArgs : EventArgs
    {
        public RequestCloseEventArgs()
        {
        }

        public RequestCloseEventArgs(bool dialogResult)
        {
            this.DialogResult = dialogResult;
        }

        public bool? DialogResult { get; set; }
    }
}