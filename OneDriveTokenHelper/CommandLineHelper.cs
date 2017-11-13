namespace OneDriveTokenHelper
{
    using System;
    using System.Collections.Generic;

    public static class CommandLineHelper
    {
        public static Dictionary<string, string> ParseCommandLineArgs(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                return new Dictionary<string, string>();
            }

            Dictionary<string, string> arguments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < args.Length; i++)
            {
                string nextArg = null;

                // Skip args that dont start with '/' or '-'
                if (!(args[i].StartsWith("/", StringComparison.OrdinalIgnoreCase) || args[i].StartsWith("-", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                // Get the name of the arg. Grab everything past the first character
                string argName = args[i].Substring(1);

                // If there is something else in the argument list after this one
                if (i + 1 < args.Length)
                {
                    nextArg = args[i + 1];

                    // If the next arg starts with a '/' or a '-', then ignore it
                    if (nextArg.StartsWith("/", StringComparison.OrdinalIgnoreCase) || nextArg.StartsWith("-", StringComparison.OrdinalIgnoreCase))
                    {
                        nextArg = null;
                    }
                }

                arguments.Add(argName, nextArg);
            }

            return arguments;
        }
    }
}