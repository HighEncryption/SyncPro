namespace SyncPro.UI.Framework.Validation
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;

    /// <summary>
    /// Custom implementation of the INotifyDataErrorInfo class.
    /// </summary>
    /// <remarks>
    /// This is needed as a custom implementation in order to support additional attribute that arent part of INotifyDataErrorInfo and 
    /// to suppress the default ErrorTemplate behavior in WPF
    /// </remarks>
    public interface IValidationBase : INotifyPropertyChanged
    {
        bool HasErrors { get; }

        event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

        IEnumerable<ValidationResult> GetErrors(string propertyName);

        void Revalidate(bool recursive);
    }
}