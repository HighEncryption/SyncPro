namespace SyncPro.UI.Framework.Validation
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Reflection;

    using SyncPro.UI.Annotations;

    public class ValidationBase : IValidationBase
    {
        public Guid TrackingId { get; }

        protected Dictionary<string, IList<IPropertyValidationRule>> PropertyValidationRules { get; }

        public bool LoadingComplete { get; set; }

        [DebuggerStepThrough]
        protected ValidationBase(bool disablePropertyValidation)
        {
            this.PropertyValidationRules = new Dictionary<string, IList<IPropertyValidationRule>>();

            if (disablePropertyValidation)
            {
                return;
            }

            this.TrackingId = Guid.NewGuid();

            LoggerExtensions.LogPropertyValidationInfo(
                "Instantiating ValidationBase with type {0} and Id {1}", this.GetType().Name, this.TrackingId);

            foreach (PropertyInfo propInfo in this.GetType().GetProperties())
            {
                if (TypeHelper.PropertyImplementsInterface(propInfo, typeof(IValidationBase)))
                {
                    // This property directly supports IValidationBase, so refer to the element for its own validation
                    {
                        foreach (object obj in propInfo.GetCustomAttributes(typeof(IPropertyValidationRule), true))
                        {
                            IPropertyValidationRule validationAttribute = obj as IPropertyValidationRule;
                            if (validationAttribute != null)
                            {
                                this.AddValidationRule(propInfo, validationAttribute);
                            }
                        }
                    }

                    continue;
                }

                // Examine the current class for any properties that have validation rule attributes declared
                foreach (IPropertyValidationRule rule in
                    propInfo.GetCustomAttributes(typeof(IPropertyValidationRule), true).OfType<IPropertyValidationRule>())
                {
                    this.AddValidationRule(propInfo, rule);
                }
            }
        }

        private void AddValidationRule(PropertyInfo propertyInfo, IPropertyValidationRule rule)
        {
            IList<IPropertyValidationRule> rulesForProperty;

            if (!this.PropertyValidationRules.TryGetValue(propertyInfo.Name, out rulesForProperty))
            {
                this.PropertyValidationRules.Add(propertyInfo.Name, rulesForProperty = new List<IPropertyValidationRule>());
            }

            rulesForProperty.Add(rule);

            LoggerExtensions.LogPropertyValidationInfo(
                "Adding validation rule {0} with Id {1} for property {2} on element {3}",
                rule.GetType().Name,
                rule.RuleInstanceId,
                propertyInfo.Name,
                this.GetType().Name);
        }

        protected bool EvaluateRulesForPropertyChange(string propertyName, object newValue)
        {
            foreach (IPropertyValidationRule rule in this.PropertyValidationRules
                .Where(r => r.Key == propertyName || r.Value.Any(r2 => r2.ValidateOnAnyPropertyChange))
                .SelectMany(pair => pair.Value))
            {
                if (rule.WaitForInitialValidation && !this.LoadingComplete)
                {
                    continue;
                }

                ValidationResult result;
                if ((result = rule.Validate(newValue, this, propertyName)) != null)
                {
                    LoggerExtensions.LogPropertyValidationInfo(
                        "ValidationRule {0} with ID {1} failed validation",
                        rule.GetType().Name,
                        rule.RuleInstanceId);

                    this.AddValidationResult(result, rule);
                }
                else
                {
                    this.RemoveValidationResult(propertyName, rule);
                }
            }

            this.RaisePropertyChanged(nameof(this.HasErrors));

            return false;
        }

        public void Revalidate(bool recursive = true)
        {
            foreach (KeyValuePair<string, IList<IPropertyValidationRule>> ruleSet in this.PropertyValidationRules)
            {
                PropertyInfo propertyInfo = this.GetType().GetProperty(ruleSet.Key);
                Debug.Assert(propertyInfo != null, "propertyInfo != null");

                object propertyValue = propertyInfo.GetValue(this);

                if (recursive)
                {
                    IValidationBase validationBase = propertyValue as IValidationBase;
                    validationBase?.Revalidate(true);
                }

                this.EvaluateRulesForPropertyChange(ruleSet.Key, propertyValue);
            }
        }

        #region IValidationBase members

        public IEnumerable<ValidationResult> GetErrors(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                return null;
            }

            lock (this.validationErrorsLock)
            {
                if (this.validationErrors.ContainsKey(propertyName))
                {
                    return this.validationErrors[propertyName];
                }
            }

            return null;
        }

        public bool HasErrors
        {
            get
            {
                lock (this.validationErrorsLock)
                {
                    return this.validationErrors.Any();
                }
            }
        }

        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

        private volatile object validationErrorsLock = new object();

        private readonly Dictionary<string, ICollection<ValidationResult>> validationErrors = new Dictionary<string, ICollection<ValidationResult>>();

        #endregion

        private void AddValidationResult(ValidationResult result, IPropertyValidationRule rule)
        {
            lock (this.validationErrorsLock)
            {
                if (!this.validationErrors.ContainsKey(result.PropertyName))
                {
                    this.validationErrors[result.PropertyName] = new List<ValidationResult>();
                }

                this.validationErrors[result.PropertyName].Add(result);
            }

            LoggerExtensions.LogPropertyValidationInfo(
                "AddValidationResult: Added result for rule {0} ({1}) on property {2}",
                rule.GetType().Name,
                result.RuleInstanceId,
                result.PropertyName);

            this.ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(result.PropertyName));
        }

        private void RemoveValidationResult(string propertyName, IPropertyValidationRule rule)
        {
            lock (this.validationErrorsLock)
            {
                KeyValuePair<string, ICollection<ValidationResult>> errorSet =
                    this.validationErrors.FirstOrDefault(pair => pair.Key == propertyName);

                if (errorSet.Key == null)
                {
                    // There arent any validation validation errors for this property. This can be expected when a
                    // rule deems a property's value a 'valid' when it is already valid (such as the initial value).
                    return;
                }

                ValidationResult error = errorSet.Value.FirstOrDefault(e => e.RuleInstanceId == rule.RuleInstanceId);

                if (error == null)
                {
                    LoggerExtensions.LogPropertyValidationInfo(
                        "RemoveValidationResult: Failed to remove validation result for rule {0} ({1}) because it was not found in the collection for element {2} of property {3}",
                        rule.GetType().Name,
                        rule.RuleInstanceId,
                        this.TrackingId,
                        propertyName);

                    return;
                }

                errorSet.Value.Remove(error);

                if (!errorSet.Value.Any())
                {
                    this.validationErrors.Remove(errorSet.Key);
                }

                LoggerExtensions.LogPropertyValidationInfo(
                    "RemoveValidationResult: Removed result for rule {0} ({1}) on property {2}",
                    rule.GetType().Name,
                    error.RuleInstanceId,
                    error.PropertyName);

                this.ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        [SuppressMessage("Microsoft.Design", "CA1030:UseEventsWhereAppropriate", Justification = "Protected method is specifically for raising the event.")]
        protected void RaisePropertyChanged(string propertyName)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
