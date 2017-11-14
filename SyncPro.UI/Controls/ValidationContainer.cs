namespace SyncPro.UI.Controls
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Linq;
    using System.Reflection;
    using System.Windows;
    using System.Windows.Controls;

    using SyncPro.UI.Framework.Validation;
    using ValidationResult = SyncPro.UI.Framework.Validation.ValidationResult;

    public class ValidationContainer : ContentControl
    {
        static ValidationContainer()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ValidationContainer),
                new FrameworkPropertyMetadata(typeof(ValidationContainer)));
        }

        public ValidationContainer()
        {
            this.Unloaded += (sender, args) =>
            {
                if (this.validationBase != null)
                {
                    this.validationBase.ErrorsChanged -= this.ValidationBaseOnErrorsChanged;
                }
            };
        }

        public bool IsRequired
        {
            get { return (bool) this.GetValue(IsRequiredProperty); }
            set { this.SetValue(IsRequiredProperty, value); }
        }

        public static readonly DependencyProperty IsRequiredProperty = DependencyProperty.Register(
            "IsRequired",
            typeof(bool),
            typeof(ValidationContainer),
            new PropertyMetadata(null));

        public bool HasValidationErrors
        {
            get { return (bool)this.GetValue(HasValidationErrorsProperty); }
            set { this.SetValue(HasValidationErrorsProperty, value); }
        }

        public static readonly DependencyProperty HasValidationErrorsProperty = DependencyProperty.Register(
            "HasValidationErrors",
            typeof(bool),
            typeof(ValidationContainer),
            new PropertyMetadata(null));

        public ObservableCollection<ValidationResult> Errors
        {
            get { return (ObservableCollection<ValidationResult>)this.GetValue(ErrorsProperty); }
            set { this.SetValue(ErrorsProperty, value); }
        }

        public static readonly DependencyProperty ErrorsProperty = DependencyProperty.Register(
            "Errors",
            typeof(ObservableCollection<ValidationResult>),
            typeof(ValidationContainer),
            new PropertyMetadata(new ObservableCollection<ValidationResult>()));

        private IValidationBase validationBase;

        private string validationPropertyName;

        public string ValidationPropertyName
        {
            get { return this.validationPropertyName; }
            set
            {
                this.validationPropertyName = value;
                this.UpdateValidationSource();
            }
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            if (e.Property == DataContextProperty)
            {
                this.UpdateValidationSource();
            }

            base.OnPropertyChanged(e);
        }

        private void UpdateValidationSource()
        {
            if (this.validationBase != null)
            {
                this.validationBase.ErrorsChanged -= this.ValidationBaseOnErrorsChanged;
                this.validationBase = null;
                this.validationPropertyName = null;
            }

            if (this.DataContext == null || string.IsNullOrEmpty(this.ValidationPropertyName))
            {
                return;
            }

            PropertyInfo validationPropertyInfo = this.DataContext.GetType().GetProperty(this.ValidationPropertyName);

            if (validationPropertyInfo == null)
            {
                throw new InvalidOperationException(
                    string.Format(
                        "Validation source type {0} does not contain a property with name {1}",
                        this.DataContext.GetType().Name,
                        this.ValidationPropertyName));
            }

            this.validationBase = this.DataContext as IValidationBase;
            if (this.validationBase == null)
            {
                return;
            }

            this.validationBase.ErrorsChanged += this.ValidationBaseOnErrorsChanged;
            this.ValidationBaseOnErrorsChanged(null, new DataErrorsChangedEventArgs(this.ValidationPropertyName));
        }

        private void ValidationBaseOnErrorsChanged(object sender, DataErrorsChangedEventArgs dataErrorsChangedEventArgs)
        {
            if (dataErrorsChangedEventArgs.PropertyName == this.validationPropertyName)
            {
                IEnumerable propertyErrors = this.validationBase.GetErrors(this.validationPropertyName) ??
                                             new List<ValidationResult>();
                var validationResults = propertyErrors.Cast<Framework.Validation.ValidationResult>().ToList();
                this.HasValidationErrors = validationResults.Any();

                this.Errors.Clear();
                foreach (ValidationResult validationResult in validationResults)
                {
                    this.Errors.Add(validationResult);
                }
            }
        }

    }
}