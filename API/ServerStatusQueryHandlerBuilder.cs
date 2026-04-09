namespace DedicatedServerMod.API
{
    /// <summary>
    /// Builds a server status-query handler registration.
    /// </summary>
    public sealed class ServerStatusQueryHandlerBuilder
    {
        internal ServerStatusQueryHandlerBuilder(string registrationId)
        {
            RegistrationId = string.IsNullOrWhiteSpace(registrationId)
                ? throw new ArgumentException("Registration id cannot be empty.", nameof(registrationId))
                : registrationId.Trim();
        }

        /// <summary>
        /// Gets the stable registration identifier.
        /// </summary>
        public string RegistrationId { get; }

        /// <summary>
        /// Gets the configured execution priority. Higher values run earlier.
        /// </summary>
        public int Priority { get; private set; }

        internal Action<ServerStatusQueryContext> HandlerCallback { get; private set; }

        /// <summary>
        /// Sets the execution priority for this registration. Higher values run earlier.
        /// </summary>
        /// <param name="priority">Execution priority.</param>
        /// <returns>The current builder.</returns>
        public ServerStatusQueryHandlerBuilder WithPriority(int priority)
        {
            Priority = priority;
            return this;
        }

        /// <summary>
        /// Sets the callback that may handle an incoming status-query request.
        /// </summary>
        /// <param name="callback">Handler callback.</param>
        /// <returns>The current builder.</returns>
        public ServerStatusQueryHandlerBuilder WithHandler(Action<ServerStatusQueryContext> callback)
        {
            HandlerCallback = callback ?? throw new ArgumentNullException(nameof(callback));
            return this;
        }
    }
}
