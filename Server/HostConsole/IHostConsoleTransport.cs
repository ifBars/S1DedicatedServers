namespace DedicatedServerMod.Server.HostConsole
{
    /// <summary>
    /// Represents a host console transport that can accept raw command lines.
    /// </summary>
    internal interface IHostConsoleTransport : IDisposable
    {
        /// <summary>
        /// Gets the transport display name for diagnostics.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Starts the transport.
        /// </summary>
        void Start();
    }
}
