#if SERVER
using DedicatedServerMod.Server.Network;

namespace DedicatedServerMod.API.Server
{
    /// <summary>
    /// Represents a registered server status-query handler.
    /// </summary>
    public sealed class ServerStatusQueryRegistration : IDisposable
    {
        private ServerStatusQueryService _owner;
        private readonly Guid _token;

        internal ServerStatusQueryRegistration(ServerStatusQueryService owner, Guid token, string registrationId)
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
        /// Unregisters this status-query handler.
        /// </summary>
        public void Dispose()
        {
            ServerStatusQueryService owner = _owner;
            if (owner == null)
            {
                return;
            }

            _owner = null;
            owner.UnregisterHandler(_token);
        }
    }
}
#endif
