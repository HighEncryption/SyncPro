namespace SyncPro.UnitTests
{
    public static class GlobalTestSettings
    {
        /// <summary>
        /// Indicates whether tests requiring network calls should be run.
        /// </summary>
        /// <remarks>
        /// A number of unit tests will communicate with a service's network API (usually HTTP) to test the 
        /// functionality of various methods. These tests require that an account exist with the given service
        /// provider, and will use a certain amount of bandwith to perform the tests. Before enabling this flag,
        /// be sure to read and understand what is required to run the test.
        /// </remarks>
        public static bool RunNetworkTests = true;

        /// <summary>
        /// Message used to indicate that network-based unit tests are disabled.
        /// </summary>
        public static string NetworkTestsDisabledMessage =
            "Network-based tests are disabled. See the code comments for the " + nameof(GlobalTestSettings) + "."
            + nameof(RunNetworkTests) + " variable so see how to enable network tests that require network communication.";
    }
}