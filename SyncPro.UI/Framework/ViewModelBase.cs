namespace SyncPro.UI.Framework
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.CompilerServices;

    using SyncPro.UI.Framework.Validation;

    public class ViewModelBase : ValidationBase, IViewModel
    {
        public event PropertyChangingEventHandler PropertyChanging;

        [DebuggerStepThrough]
        protected ViewModelBase()
            : this(false)
        {
        }

        [DebuggerStepThrough]
        protected ViewModelBase(bool enablePropertyValidation)
            : base(!enablePropertyValidation)
        {
        }

        #region Property Change Support

        [SuppressMessage("Microsoft.Design", "CA1030:UseEventsWhereAppropriate", Justification = "Protected method is specifically for raising the event.")]
        protected void RaisePropertyChanging(string property)
        {
            this.PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(property));
        }

        [SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", Justification = "Design is specific to ViewModel pattern.")]
        protected bool SetProperty<T>(ref T property, T newValue, [CallerMemberName] string propertyName = "")
        {
            return this.SetPropertyInternal(propertyName, ref property, newValue);
        }

        [SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", Justification = "Design is specific to ViewModel pattern.")]
        private bool SetPropertyInternal<T>(string propertyName, ref T property, T newValue)
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

            if (this.EvaluateRulesForPropertyChange(propertyName, newValue))
            {
                return false;
            }

            this.RaisePropertyChanging(propertyName);

            IValidationBase oldVal = property as IValidationBase;
            if (oldVal != null)
            {
                oldVal.ErrorsChanged -= this.ChildElementErrorsChanged;
            }

            IValidationBase newVal = newValue as IValidationBase;
            if (newVal != null)
            {
                newVal.ErrorsChanged += this.ChildElementErrorsChanged;
            }

            property = newValue;

            this.RaisePropertyChanged(propertyName);

            return true;
        }

        private void ChildElementErrorsChanged(object sender, DataErrorsChangedEventArgs e)
        {
            this.Revalidate(false);
        }

        internal bool SetPropertyDelegated<T>(
            string propertyName, 
            T property, 
            T newValue, 
            Action setProperty,
            [CallerMemberName] string callerPropertyName = "")
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                throw new ArgumentNullException(nameof(propertyName));
            }

            if (!IsDifferent(property, newValue))
            {
                return false;
            }

            if (this.EvaluateRulesForPropertyChange(callerPropertyName, newValue))
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
            : this(model, false)
        {
        }

        protected ViewModelBase(TModel model, bool enablePropertyValidation)
            : base(enablePropertyValidation)
        {
            this.BaseModel = model;
        }

        public abstract void LoadContext();

        public abstract void SaveContext();
    }
}