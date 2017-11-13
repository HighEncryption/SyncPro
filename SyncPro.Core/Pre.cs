namespace SyncPro
{
    using System;
    using System.Diagnostics;

    using JetBrains.Annotations;

    public static class Pre
    {
        [DebuggerStepThrough]
        public static void ThrowIfArgumentNull(object arg, string name)
        {
            if (arg == null)
            {
                throw new ArgumentNullException(name);
            }
        }

        [DebuggerStepThrough]
        public static void ThrowIfStringNullOrEmpty(string arg, string name)
        {
            if (string.IsNullOrEmpty(arg))
            {
                throw new ArgumentNullException(name);
            }
        }

        [DebuggerStepThrough]
        public static void ThrowIfStringNullOrWhiteSpace(string arg, string name)
        {
            if (string.IsNullOrWhiteSpace(arg))
            {
                throw new ArgumentNullException(name);
            }
        }

        public static void ThrowIfFalse(bool value, string message)
        {
            if (!value)
            {
                throw new ArgumentException(message);
            }
        }

        public static void ThrowIfTrue(bool value, string message)
        {
            if (value)
            {
                throw new ArgumentException(message);
            }
        }

        [ContractAnnotation("condition:false=>halt")]
        public static void Assert(bool condition)
        {
            Pre.Assert(condition, null);
        }

        [ContractAnnotation("condition:false=>halt")]
        public static void Assert(bool condition, string message)
        {
            if (condition)
            {
                return;
            }

#if DEBUG
            Debugger.Break();
#endif

            if (message != null)
            {
                Debug.Fail(message);
            }
        }
    }
}