namespace SyncPro.UI.Framework.Validation
{
    using System;

    using JsonLog;

    public abstract class PropertyValidationRule : Attribute, IPropertyValidationRule
    {
        public string ErrorMessage { get; }

        /// <summary>
        /// Set to true when the validation logic should be executed on any property change. False when it should only be executed 
        /// when the property that the validation is declared on changes.
        /// </summary>
        public bool ValidateOnAnyPropertyChange { get; set; }

        public bool WaitForInitialValidation { get; set; }

        public Guid RuleInstanceId { get; }

        public ValidationResult Validate(object value, object context, string propertyName)
        {
            try
            {
                ValidationResult result = this.Evaluate(value, context);
                if (result != null)
                {
                    result.PropertyName = propertyName;
                }

                return result;
            }
            catch (Exception exception)
            {
                string message = "Exception thrown during property validation. " + exception.Message;
                Logger.Warning(message);
                return new ValidationExceptionResult(propertyName, message, this.RuleInstanceId, exception);
            }
        }

        protected PropertyValidationRule(string errorMessage)
        {
            this.RuleInstanceId = Guid.NewGuid();

            this.ErrorMessage = errorMessage;
        }

        protected abstract ValidationResult Evaluate(object value, object context);
    }
}