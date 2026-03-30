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
    /// Placeholder backend for Steam Web API ticket validation.
    /// </summary>
    /// <remarks>
    /// Steam game server authentication is implemented first for hosted environments.
    /// This backend keeps the provider architecture extensible for future web API support.
    /// </remarks>
    public sealed class SteamWebApiAuthBackend : IPlayerAuthBackend
    {
        /// <summary>
        /// Initializes a web API auth backend placeholder.
        /// </summary>
        public SteamWebApiAuthBackend()
        {
        }

        /// <inheritdoc />
        public AuthenticationProvider Provider => AuthenticationProvider.SteamWebApi;

        /// <inheritdoc />
        public bool IsInitialized { get; private set; }

        /// <inheritdoc />
        public AuthenticationResult Initialize()
        {
            IsInitialized = true;
            DebugLog.Warning("SteamWebApi auth backend is not implemented yet; authentication will reject joins when selected.");

            return new AuthenticationResult
            {
                IsSuccessful = true,
                Message = "SteamWebApi backend initialized in placeholder mode"
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
                    Message = "SteamWebApi authentication backend is not implemented yet",
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
