namespace SyncProLogViewer.ViewModels
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics.CodeAnalysis;

    public class ViewModelBase : IViewModel
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public event PropertyChangingEventHandler PropertyChanging;


        #region Property Change Support

        [SuppressMessage("Microsoft.Design", "CA1030:UseEventsWhereAppropriate", Justification = "Protected method is specifically for raising the event.")]
        protected void RaisePropertyChanging(string property)
        {
            this.PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(property));
        }

        [SuppressMessage("Microsoft.Design", "CA1030:UseEventsWhereAppropriate", Justification = "Protected method is specifically for raising the event.")]
        protected void RaisePropertyChanged(string property)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }

        [SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", Justification = "Design is specific to ViewModel pattern.")]
        protected bool SetProperty<T>(string propertyName, ref T property, T newValue)
        {
            return this.SetProperty(propertyName, ref property, newValue, false);
        }

        [SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", Justification = "Design is specific to ViewModel pattern.")]
        protected bool SetProperty<T>(string propertyName, ref T property, T newValue, bool revalidate)
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

        internal delegate void SetPropertyDelegate();

        internal bool SetPropertyDelegated<T>(string propertyName, T property, T newValue, SetPropertyDelegate setProperty)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                throw new ArgumentNullException(nameof(propertyName));
            }

            if (!IsDifferent(property, newValue))
            {
                return false;
            }

            this.RaisePropertyChanging(propertyName);

            setProperty();

            this.RaisePropertyChanged(propertyName);

            return true;
        }

        #endregion

        public static bool IsDifferent<T>(T currentValue, T newValue)
        {
            // ReSharper disable RedundantNameQualifier
            if (object.Equals(currentValue, default(T)) && object.Equals(newValue, default(T)))
            {
                return false;
            }

            if (!object.Equals(currentValue, default(T)) && currentValue.Equals(newValue))
            {
                return false;
            }

            // ReSharper restore RedundantNameQualifier
            return true;
        }
    }

    public abstract class ViewModelBase<TModel> : ViewModelBase, IViewModel<TModel>
        where TModel : class
    {
        protected TModel BaseModel { get; }

        protected ViewModelBase(TModel model)
        {
            this.BaseModel = model;
        }

        public abstract void LoadContext();

        public abstract void SaveContext();
    }
}
