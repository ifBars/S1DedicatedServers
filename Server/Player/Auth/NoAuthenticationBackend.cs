using DedicatedServerMod.Shared.Configuration;
using DedicatedServerMod.Utils;
#if IL2CPP
using Il2CppFishNet.Connection;
#else
using FishNet.Connection;
#endif

namespace DedicatedServerMod.Server.Player.Auth
{
    /// <summary>
    /// No-op authentication backend used when authentication is disabled.
    /// </summary>
    public sealed class NoAuthenticationBackend : IPlayerAuthBackend
    {
        /// <summary>
        /// Initializes a no-op auth backend.
        /// </summary>
        public NoAuthenticationBackend()
        {
        }

        /// <inheritdoc />
        public AuthenticationProvider Provider => AuthenticationProvider.None;

        /// <inheritdoc />
        public bool IsInitialized { get; private set; }

        /// <inheritdoc />
        public AuthenticationResult Initialize()
        {
            IsInitialized = true;
            return new AuthenticationResult
            {
                IsSuccessful = true,
                Message = "No-auth backend initialized"
            };
        }

        /// <inheritdoc />
        public AuthBeginResult BeginAuthentication(NetworkConnection connection, Shared.Networking.AuthTicketMessage ticketMessage)
        {
            return new AuthBeginResult
            {
                IsPending = false,
                ImmediateResult = new AuthenticationResult
                {
                    IsSuccessful = true,
                    Message = "Authentication bypassed by configuration"
                }
            };
        }

        /// <inheritdoc />
        public void Tick()
        {
        }

        /// <inheritdoc />
        public IReadOnlyList<AuthCompletion> DrainCompletions()
        {
            return Array.Empty<AuthCompletion>();
        }

        /// <inheritdoc />
        public void EndSession(string steamId)
        {
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            if (IsInitialized)
            {
                DebugLog.AuthenticationDebug("No-auth backend shutdown");
            }

            IsInitialized = false;
        }
    }
}
