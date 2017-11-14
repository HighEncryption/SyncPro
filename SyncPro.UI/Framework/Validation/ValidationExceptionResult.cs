namespace SyncPro.UI.Framework.Validation
{
    using System;

    public class ValidationExceptionResult : ValidationResult
    {
        public Exception Exception { get; }

        public ValidationExceptionResult(string propertyName, string message, Guid ruleInstanceId, Exception exception) 
            : base(propertyName, message, ruleInstanceId)
        {
            this.Exception = exception;
        }
    }
}