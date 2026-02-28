using System;
using System.Collections.Generic;
#if IL2CPP
using Il2CppFishNet.Connection;
#else
using FishNet.Connection;
#endif
using DedicatedServerMod.Shared.Configuration;

namespace DedicatedServerMod.Server.Player.Auth
{
    /// <summary>
    /// Contract for pluggable player authentication backends.
    /// </summary>
    public interface IPlayerAuthBackend
    {
        /// <summary>
        /// Gets the provider served by this backend implementation.
        /// </summary>
        AuthenticationProvider Provider { get; }

        /// <summary>
        /// Gets whether the backend is initialized and available.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Initializes backend resources.
        /// </summary>
        /// <returns>Initialization result details.</returns>
        AuthenticationResult Initialize();

        /// <summary>
        /// Starts authenticating a connection with the provided ticket payload.
        /// </summary>
        /// <param name="connection">Connection being authenticated.</param>
        /// <param name="ticketMessage">Client ticket payload.</param>
        /// <returns>Immediate begin result.</returns>
        AuthBeginResult BeginAuthentication(NetworkConnection connection, Shared.Networking.AuthTicketMessage ticketMessage);

        /// <summary>
        /// Pumps backend callbacks and internal processing.
        /// </summary>
        void Tick();

        /// <summary>
        /// Drains completed authentication callbacks produced since last tick.
        /// </summary>
        /// <returns>Completed authentication records.</returns>
        IReadOnlyList<AuthCompletion> DrainCompletions();

        /// <summary>
        /// Ends an active authentication session for a player.
        /// </summary>
        /// <param name="steamId">SteamID associated with the active session.</param>
        void EndSession(string steamId);

        /// <summary>
        /// Shuts down backend resources.
        /// </summary>
        void Shutdown();
    }

    /// <summary>
    /// Result returned by BeginAuthentication.
    /// </summary>
    public sealed class AuthBeginResult
    {
        /// <summary>
        /// Whether authentication is now pending and waiting for async completion.
        /// </summary>
        public bool IsPending { get; set; }

        /// <summary>
        /// Immediate terminal result when IsPending is false.
        /// </summary>
        public AuthenticationResult ImmediateResult { get; set; }
    }

    /// <summary>
    /// Completed backend authentication callback result.
    /// </summary>
    public sealed class AuthCompletion
    {
        /// <summary>
        /// Connection associated with the completed authentication.
        /// </summary>
        public NetworkConnection Connection { get; set; }

        /// <summary>
        /// Authentication result for the associated connection.
        /// </summary>
        public AuthenticationResult Result { get; set; }
    }
}
