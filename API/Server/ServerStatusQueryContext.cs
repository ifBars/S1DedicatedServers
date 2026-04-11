namespace DedicatedServerMod.API.Server
{
    /// <summary>
    /// Provides context for a server status-query handler invocation.
    /// </summary>
    public sealed class ServerStatusQueryContext
    {
        internal ServerStatusQueryContext(string requestLine)
        {
            RequestLine = requestLine ?? string.Empty;
        }

        /// <summary>
        /// Gets the raw request line received on the status-query endpoint.
        /// </summary>
        public string RequestLine { get; }

        /// <summary>
        /// Gets a value indicating whether the request has been handled.
        /// </summary>
        public bool IsHandled { get; private set; }

        /// <summary>
        /// Gets the single-line response that should be returned to the client.
        /// </summary>
        public string ResponseLine { get; private set; } = string.Empty;

        /// <summary>
        /// Marks the request as handled and supplies the response line to send.
        /// </summary>
        /// <param name="responseLine">Single-line response payload.</param>
        public void Respond(string responseLine)
        {
            ResponseLine = responseLine ?? string.Empty;
            IsHandled = true;
        }
    }
}
