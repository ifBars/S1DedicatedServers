using System.Collections;

namespace DedicatedServerMod.API
{
    /// <summary>
    /// Builds a client join-preparation registration for the dedicated-server connection pipeline.
    /// </summary>
    public sealed class ClientJoinPreparationBuilder
    {
        internal ClientJoinPreparationBuilder(string registrationId)
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

        internal Func<ClientJoinPreparationContext, IEnumerator> PrepareCallback { get; private set; }

        internal Action<ClientJoinPreparationContext> FinalizeCallback { get; private set; }

        internal Action<ClientJoinPreparationContext> ResetCallback { get; private set; }

        /// <summary>
        /// Sets the execution priority for this registration. Higher values run earlier.
        /// </summary>
        /// <param name="priority">Execution priority.</param>
        /// <returns>The current builder.</returns>
        public ClientJoinPreparationBuilder WithPriority(int priority)
        {
            Priority = priority;
            return this;
        }

        /// <summary>
        /// Sets the coroutine invoked before the transport connection begins.
        /// </summary>
        /// <param name="callback">Preparation callback.</param>
        /// <returns>The current builder.</returns>
        public ClientJoinPreparationBuilder WithPrepare(Func<ClientJoinPreparationContext, IEnumerator> callback)
        {
            PrepareCallback = callback ?? throw new ArgumentNullException(nameof(callback));
            return this;
        }

        /// <summary>
        /// Sets the callback invoked after the gameplay scene loads but before the native load continues.
        /// </summary>
        /// <param name="callback">Finalization callback.</param>
        /// <returns>The current builder.</returns>
        public ClientJoinPreparationBuilder WithFinalize(Action<ClientJoinPreparationContext> callback)
        {
            FinalizeCallback = callback ?? throw new ArgumentNullException(nameof(callback));
            return this;
        }

        /// <summary>
        /// Sets the callback invoked when transient preparation state should be reset.
        /// </summary>
        /// <param name="callback">Reset callback.</param>
        /// <returns>The current builder.</returns>
        public ClientJoinPreparationBuilder WithReset(Action<ClientJoinPreparationContext> callback)
        {
            ResetCallback = callback ?? throw new ArgumentNullException(nameof(callback));
            return this;
        }
    }
}
