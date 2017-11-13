namespace SyncPro
{
    using System;
    using System.ComponentModel;
    using System.Runtime.CompilerServices;

    public class NotifyPropertyChangedSlim : INotifyPropertyChanged, INotifyPropertyChanging
    {
        public event PropertyChangingEventHandler PropertyChanging;
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void RaisePropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = this.PropertyChanged;
            handler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual void RaisePropertyChanging(string propertyName)
        {
            PropertyChangingEventHandler handler = this.PropertyChanging;
            handler?.Invoke(this, new PropertyChangingEventArgs(propertyName));
        }

        protected bool SetProperty<T>(
            ref T property, 
            T newValue, 
            [CallerMemberName] string propertyName = "")
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                throw new ArgumentNullException(nameof(propertyName));
            }

            // ReSharper disable RedundantNameQualifier
            if (object.Equals(property, default(T)) && object.Equals(newValue, default(T)))
            {
                return false;
            }

            if (!object.Equals(property, default(T)) && property.Equals(newValue))
            {
                return false;
            }

            // ReSharper restore RedundantNameQualifier
            this.RaisePropertyChanging(propertyName);

            property = newValue;

            this.RaisePropertyChanged(propertyName);

            return true;
        }

        internal bool SetPropertyDelegated<T>(
            T property,
            T newValue,
            Action setProperty,
            [CallerMemberName] string propertyName = "")
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                throw new ArgumentNullException(nameof(propertyName));
            }

            // ReSharper disable RedundantNameQualifier
            if (object.Equals(property, default(T)) && object.Equals(newValue, default(T)))
            {
                return false;
            }

            this.RaisePropertyChanging(propertyName);

            setProperty();

            this.RaisePropertyChanged(propertyName);

            return true;
        }
    }
}