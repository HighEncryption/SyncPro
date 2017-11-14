namespace SyncPro.UI.Framework.Validation.Rules
{
    public class StringNotNullorEmptyValidationRuleAttribute : PropertyValidationRule
    {
        public StringNotNullorEmptyValidationRuleAttribute()
            : base("The value cannot be empty.")
        {
        }

        protected override ValidationResult Evaluate(object value, object context)
        {
            string strValue = (string) value;
            return ValidationResult.FromRule(this, !string.IsNullOrEmpty(strValue));
        }
    }
}
