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
    /// Deprecated placeholder backend for legacy Steam Web API ticket validation.
    /// </summary>
    /// <remarks>
    /// Steam game server authentication is the supported hosted-server path.
    /// This backend is retained only for binary/source compatibility and is no longer selected by configuration.
    /// </remarks>
    internal sealed class SteamWebApiAuthBackend : IPlayerAuthBackend
    {
        /// <summary>
        /// Initializes a web API auth backend placeholder.
        /// </summary>
        public SteamWebApiAuthBackend()
        {
        }

        /// <inheritdoc />
#pragma warning disable CS0618 // Backend is retained only to represent the deprecated provider value.
        public AuthenticationProvider Provider => AuthenticationProvider.SteamWebApi;
#pragma warning restore CS0618

        /// <inheritdoc />
        public bool IsInitialized { get; private set; }

        /// <inheritdoc />
        public AuthenticationResult Initialize()
        {
            IsInitialized = true;
            DebugLog.Warning("SteamWebApi auth backend is deprecated and not selected by configuration. Use SteamGameServer.");

            return new AuthenticationResult
            {
                IsSuccessful = true,
                Message = "SteamWebApi backend initialized as deprecated compatibility placeholder"
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
                    IsSuccessful = false,
                    Message = "SteamWebApi authentication backend is deprecated; use SteamGameServer",
                    ShouldDisconnect = true
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
            IsInitialized = false;
        }
    }
}
