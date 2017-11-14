namespace SyncPro.UI.Framework.Validation
{
    using System;

    public interface IPropertyValidationRule
    {
        bool ValidateOnAnyPropertyChange { get; set; }

        Guid RuleInstanceId { get; }

        string ErrorMessage { get; }

        bool WaitForInitialValidation { get; set; }


        ValidationResult Validate(object value, object context, string propertyName);
    }
}