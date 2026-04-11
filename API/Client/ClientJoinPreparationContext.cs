namespace DedicatedServerMod.API.Client
{
    /// <summary>
    /// Provides context for a client join-preparation registration during a dedicated-server connection attempt.
    /// </summary>
    public sealed class ClientJoinPreparationContext
    {
        internal ClientJoinPreparationContext(string host, int port)
        {
            Host = host ?? string.Empty;
            Port = port;
        }

        /// <summary>
        /// Gets the dedicated server host currently being joined.
        /// </summary>
        public string Host { get; }

        /// <summary>
        /// Gets the dedicated server port currently being joined.
        /// </summary>
        public int Port { get; }

        /// <summary>
        /// Gets the failure reason reported by the registration, if any.
        /// </summary>
        public string FailureReason { get; private set; } = string.Empty;

        /// <summary>
        /// Gets a value indicating whether the registration reported a failure for the current join attempt.
        /// </summary>
        public bool HasFailed =>
            !string.IsNullOrWhiteSpace(FailureReason);

        /// <summary>
        /// Marks the current join-preparation stage as failed with a user-facing reason.
        /// </summary>
        /// <param name="reason">Failure reason shown when the join is aborted.</param>
        public void Fail(string reason)
        {
            FailureReason = string.IsNullOrWhiteSpace(reason)
                ? "Join preparation failed."
                : reason.Trim();
        }

        internal void ClearFailure()
        {
            FailureReason = string.Empty;
        }
    }
}
