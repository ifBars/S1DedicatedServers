#if CLIENT
using DedicatedServerMod.Client.Managers;

namespace DedicatedServerMod.API
{
    /// <summary>
    /// Represents a registered client join-preparation callback set.
    /// </summary>
    public sealed class ClientJoinPreparationRegistration : IDisposable
    {
        private ClientConnectionManager _owner;
        private readonly Guid _token;

        internal ClientJoinPreparationRegistration(ClientConnectionManager owner, Guid token, string registrationId)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _token = token;
            RegistrationId = registrationId ?? string.Empty;
        }

        /// <summary>
        /// Gets the stable registration identifier.
        /// </summary>
        public string RegistrationId { get; }

        /// <summary>
        /// Unregisters this join-preparation registration.
        /// </summary>
        public void Dispose()
        {
            ClientConnectionManager owner = _owner;
            if (owner == null)
            {
                return;
            }

            _owner = null;
            owner.UnregisterJoinPreparation(_token);
        }
    }
}
#endif
