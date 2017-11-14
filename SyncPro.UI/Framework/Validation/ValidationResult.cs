namespace SyncPro.UI.Framework.Validation
{
    using System;

    public class ValidationResult
    {
        public string Message { get; set; }

        public string PropertyName { get; set; }

        public Guid RuleInstanceId { get; set; }

        public ValidationResult(string propertyName, string message, Guid ruleInstanceId)
        {
            this.PropertyName = propertyName;
            this.Message = message;
            this.RuleInstanceId = ruleInstanceId;
        }

        public static ValidationResult FromRule(PropertyValidationRule rule, bool condition)
        {
            if (condition)
            {
                return null;
            }

            // Return a result with a null property name. This will be replaces with the correct value later on, since
            // this is only expected to be called from within a PropertyValidationRule.Evaluate method.
            return new ValidationResult(null, rule.ErrorMessage, rule.RuleInstanceId);
        }
    }
}