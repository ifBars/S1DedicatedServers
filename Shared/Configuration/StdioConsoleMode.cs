namespace DedicatedServerMod.Shared.Configuration
{
    /// <summary>
    /// Controls activation of the stdio host console transport.
    /// </summary>
    public enum StdioConsoleMode
    {
        /// <summary>
        /// Never start the stdio host console transport.
        /// </summary>
        Disabled,

        /// <summary>
        /// Start the stdio host console transport only when stdin is redirected.
        /// </summary>
        Auto,

        /// <summary>
        /// Always start the stdio host console transport.
        /// </summary>
        Enabled
    }
}
