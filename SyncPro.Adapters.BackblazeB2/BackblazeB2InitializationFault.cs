namespace SyncPro.Adapters.BackblazeB2
{
    using System;

    public class BackblazeB2InitializationFault : AdapterFaultInformation
    {
        public BackblazeB2Adapter Adapter { get; }
        public Exception Exception { get; }

        public BackblazeB2InitializationFault(BackblazeB2Adapter adapter, Exception exception)
        {
            this.Adapter = adapter;
            this.Exception = exception;
        }
    }
}