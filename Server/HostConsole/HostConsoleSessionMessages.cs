namespace DedicatedServerMod.Server.HostConsole
{
    /// <summary>
    /// Defines the TCP host console session messages that are emitted when a client connects.
    /// </summary>
    internal static class HostConsoleSessionMessages
    {
        /// <summary>
        /// Preserves the established host-panel readiness signal.
        /// </summary>
        // Cybrancee marks the server as running when it detects this exact line after TCP console connection.
        // Keep the wording intact and emit it before extended command-session guidance.
        internal const string ReadyForCommands = "Type 'help' for commands.";

        /// <summary>
        /// Introduces the extended command-session capabilities.
        /// </summary>
        internal const string CommandSessionHint = "Command session. Try: help, serverinfo, logs [lines], tail [lines].";

        /// <summary>
        /// Explains how to retrieve output when a host panel does not stream stdout.
        /// </summary>
        internal const string LogRetrievalHint = "If your host panel does not stream stdout, use logs or tail for recent server output.";
    }
}
