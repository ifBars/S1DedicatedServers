using System;
using System.Collections.Generic;
using DedicatedServerMod.Shared.Configuration;
using FishNet.Connection;
using MelonLoader;

namespace DedicatedServerMod.Server.Player.Auth
{
    /// <summary>
    /// No-op authentication backend used when authentication is disabled.
    /// </summary>
    public sealed class NoAuthenticationBackend : IPlayerAuthBackend
    {
        private readonly MelonLogger.Instance _logger;

        /// <summary>
        /// Initializes a no-op auth backend.
        /// </summary>
        /// <param name="logger">Logger instance.</param>
        public NoAuthenticationBackend(MelonLogger.Instance logger)
        {
            _logger = logger;
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
                _logger.Msg("No-auth backend shutdown");
            }

            IsInitialized = false;
        }
    }
}
