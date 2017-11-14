namespace SyncPro.UI.Framework.Validation.Rules
{
    public class ChildElementValidationRuleAttribute : PropertyValidationRule
    {
        public ChildElementValidationRuleAttribute() 
            : base("Child element contains errors.")
        {
        }

        protected override ValidationResult Evaluate(object value, object context)
        {
            IValidationBase validationBase = value as IValidationBase;
            if (validationBase == null)
            {
                return null;
            }

            return ValidationResult.FromRule(this, !validationBase.HasErrors);
        }
    }
}