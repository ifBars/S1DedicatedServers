using System;
using System.Collections.Generic;
using DedicatedServerMod.Shared.Configuration;
#if IL2CPP
using Il2CppFishNet.Connection;
#else
using FishNet.Connection;
#endif
using MelonLoader;

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
        private readonly MelonLogger.Instance _logger;

        /// <summary>
        /// Initializes a web API auth backend placeholder.
        /// </summary>
        /// <param name="logger">Logger instance.</param>
        public SteamWebApiAuthBackend(MelonLogger.Instance logger)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public AuthenticationProvider Provider => AuthenticationProvider.SteamWebApi;

        /// <inheritdoc />
        public bool IsInitialized { get; private set; }

        /// <inheritdoc />
        public AuthenticationResult Initialize()
        {
            IsInitialized = true;
            _logger.Warning("SteamWebApi auth backend is not implemented yet; authentication will reject joins when selected.");

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
